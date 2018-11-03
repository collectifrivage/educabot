using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Educadev
{
    public static class Extensions
    {
        public static Task SendToQueue(this IBinder binder, string queueName, object message)
        {
            return binder.SendToQueue(queueName, JsonConvert.SerializeObject(message));
        }

        public static async Task SendToQueue(this IBinder binder, string queueName, string message)
        {
            var attr = new QueueAttribute(queueName);
            var queue = await binder.BindAsync<CloudQueue>(attr);
            await queue.AddMessageAsync(new CloudQueueMessage(message));
        }

        public static async Task<CloudTable> GetTable(this IBinder binder, string tableName)
        {
            var attr = new TableAttribute(tableName);
            return await binder.BindAsync<CloudTable>(attr);
        }

        public static async Task<IList<T>> ExecuteQueryAsync<T>(
            this CloudTable table, 
            TableQuery<T> query,
            CancellationToken ct = default(CancellationToken), 
            Action<IList<T>> onProgress = null)
            where T : ITableEntity, new()
        {
            var items = new List<T>();
            TableContinuationToken token = null;

            do
            {
                var seg = await table.ExecuteQuerySegmentedAsync(query, token);

                token = seg.ContinuationToken;
                items.AddRange(seg);
                onProgress?.Invoke(items);

            } while (token != null && !ct.IsCancellationRequested);

            return items;
        }

        public static async Task<IList<T>> GetAllByPartition<T>(this CloudTable table, string partitionKey) where T : ITableEntity, new()
        {
            var query = new TableQuery<T>().Where(TableQuery.GenerateFilterCondition("PartitionKey", "eq", partitionKey));
            return await table.ExecuteQueryAsync(query);
        }

        public static async Task<T> Retrieve<T>(this CloudTable table, string partitionKey, string rowKey) where T : class, ITableEntity
        {
            var result = await table.ExecuteAsync(TableOperation.Retrieve<T>(partitionKey, rowKey));
            return result?.Result as T;
        }
    }
}
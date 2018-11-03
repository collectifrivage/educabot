namespace Educadev.Models.Slack.Dialogs
{
    public class TextareaDialogElement : TextDialogElement
    {
        public TextareaDialogElement(string name, string label) : base(name, label)
        {
            Type = "textarea";
        }
    }
}
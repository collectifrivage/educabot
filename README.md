# Éducabot
Bot Slack pour planifier l'écoute de vidéos éducatifs.

<a href="https://slack.com/oauth/authorize?client_id=2183849147.481443018064&scope=commands,chat:write:bot"><img alt="Add to Slack" height="40" width="139" src="https://platform.slack-edge.com/img/add_to_slack.png" srcset="https://platform.slack-edge.com/img/add_to_slack.png 1x, https://platform.slack-edge.com/img/add_to_slack@2x.png 2x" /></a>

## Pour développer

### Résumé technologique

Le bot est bâti avec Azure Functions. Les fonctions sont séparées dans 4 fichiers selon leur utilité:
- `BotActions.cs` contient une seule fonction, `DispatchAction` qui est appelée par Slack lors d'un clic sur un bouton ou la soumission d'un dialogue. La fonction vérifie le type d'intéractivité et le bouton ou dialogue spécifique et appel la bonne méthode pour gérer l'action.
- `BotCommands.cs` contient les fonctions qui sont appelées lorsqu'une _slash command_ est reçue.
- `BotTimers.cs` contient les fonctions qui sont appelées selon un horaire pré-défini.
- `BotSetup.cs` contient le callback appelé lors de l'installation de l'application.

_Azure Table Storage_ est utilisé pour tous les besoins de stockage. 

### Pré-requis
- Visual Studio 2017
- Extension [Azure Functions and Web Jobs Tools](https://marketplace.visualstudio.com/items?itemName=VisualStudioWebandAzureTools.AzureFunctionsandWebJobsTools)
- L'outil [ngrok](https://ngrok.com/download)
- Un workspace Slack pour tester l'extension

### Configuration de votre environnement
- [Créez une nouvelle app Slack](https://api.slack.com/apps?new_app=1) dans votre workspace de test.
- Dans la section _Basic Information_, notez les _Client ID_, _Client Secret_, et _Signing Secret_.
- Clonez le repository, ouvrez la solution dans Visual Studio et ajoutez un fichier à la racine du projet nommé `local.settings.json` avec le contenu suivant (en substituant les valeurs notées précédement):
  ```json
  {
    "IsEncrypted": false,
    "Values": {
      "AzureWebJobsStorage": "UseDevelopmentStorage=true",
      "FUNCTIONS_WORKER_RUNTIME": "dotnet",
      "WEBSITE_TIME_ZONE": "Eastern Standard Time",
      "ClientId": "<VOTRE CLIENT ID>", 
      "ClientSecret": "<VOTRE CLIENT SECRET>", 
      "SigningSecret": "<VOTRE SIGNING SECRET>"
    }
  }
  ```
- Exécutez l'application (exemple, en appuyant sur F5), assurez-vous qu'elle démarre avec succès
- Dans une ligne de commande, exécutez `ngrok http 7071` (7071 est le port par défaut en développement).
- Notez le nom de domaine assigné par ngrok (quelque chose comme `http://<ID RANDOM>.ngrok.io`)
- De retour dans votre app Slack, allez dans la section _Interactive Components_, activez la feature, et entrez l'URL suivante: `http://<ID RANDOM>.ngrok.io/api/slack/action-endpoint`. N'oubliez pas de sauvegarder.
- Allez dans _Slash Commands_, et créez les commandes suivantes:
  - Commande: `/edu:propose`, URL: `http://<ID RANDOM>.ngrok.io/api/slack/commands/propose`
  - Commande: `/edu:list`, URL: `http://<ID RANDOM>.ngrok.io/api/slack/commands/list`
  - Commande: `/edu:plan`, URL: `http://<ID RANDOM>.ngrok.io/api/slack/commands/plan`
  - Commande: `/edu:next`, URL: `http://<ID RANDOM>.ngrok.io/api/slack/commands/next`
- Allez dans _OAuth & Permissions_ et ajoutez le redirect URL: `http://<ID RANDOM>.ngrok.io/api/install`.
- Toujours dans _OAuth & Permissions_, assurez vous d'avoir les scopes suivants: `chat:write:bot` et `commands`.
- Finalement dans _Manage Distribution_ vous devriez voir un bouton _Add to Slack_. Cliquez dessus et autorisez l'application pour votre workspace de test. Si tout s'est bien passé, vous devriez voir le texte _All set!_. Vous devriez maintenant être en mesure d'utiliser le bot dans votre workspace.

**Attention**: ngrok vous assignera un nouveau domaine à chaque fois que vous exécutez l'outil. Il faudra donc retourner dans les paramètres de votre app pour changer les URLs à chaque fois. Je sais, c'est plate, mais c'est comme ça :P
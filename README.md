# Discord-SBot
Discord bot to pull the latest commits for the sbox game and push them to #sbox-updates

## Bot Specific Setup
- Add an environment variable named `DISCORD_SBOT_TOKEN` with the bot token.

- Create a channel in your discord named `sbox-updates`

- Create a bot in the discord developer panel with enough permissions to post embeded links

- Add the bot to your discord server.

- Launch the application

Once launch the application will be begin pulling all the commits and posting them to the channel. It will save the commits it's posted into a binary file in your local appdata folder,
and add itself to launch with windows and ping for new commits every 15 seconds.

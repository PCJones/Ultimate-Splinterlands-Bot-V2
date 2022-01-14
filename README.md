# Ultimate-Splinterlands-Bot-V2
A fast, free, multi-account splinderlands bot

A completely rewritten new version of this bot: https://github.com/PCJones/ultimate-splinterlands-bot

# Support / Community

[Discord](https://discord.gg/hwSr7KNGs9)

[Telegram](https://t.me/ultimatesplinterlandsbot) 

## Features
- Multiple accounts fighting in parallel (Multithreading)
- Super fast lightning mode that will barely use any CPU or RAM
- Smart Team Selection - the bot will chose cards with best win rate
- The bot will play for the quests, including sneak and snipe (can be disabled)
- Minimum Energy Capture Rate - the bot will pause automatically if the energy capture rate is below a specified percentage
- Option to enable/disable automatic quest reward chest opening
- And much more, see [Bot configuration](https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2#bot-configuration)
- Any suggestions?

## How to install (Windows)
- [Youtube Tutorial](https://www.youtube.com/watch?v=wVHL94ZH5r8)
- Download the windows.zip from the [Releases Page](https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2/releases) and extract it
- Install https://dotnet.microsoft.com/download/dotnet/5.0/runtime (for console applications)
- Create config.txt and accounts.txt in config folder - see config-example.txt and accounts-example.txt
- Start the bot by double clicking on Ultimate Splinterlands Bot V2.exe in main folder

## How to install (Linux / MacOS)
- [Youtube Tutorial](https://www.youtube.com/watch?v=kTS0FdAei7c)
- Chrome download (Chrome is no longer needed)
- Chromedriver download (Chromedriver is no longer needed)
- Text instructions coming soon, please ask for instructions on discord or Telegram

[Discord](https://discord.gg/hwSr7KNGs9)

[Telegram](https://t.me/ultimatesplinterlandsbot) 

# Donations

In case you want to donate to me for creating this bot, I would be very happy!

- DEC/SPS into the game to the player **pcjones** 
- Bitcoin 3Dri7HkoP2UyGEjPhMV7LXJyRYZkJ2bygT
- Ethereum 0xcFE8c78F07e0190EBdD9077cF9d9E3A8DCED8d91 
- WAX to account **lshru.wam** (please copy the name)
- BUSD/USDT/BNB etc (Binance Smart Chain) 0x951844e1525bf37f36d7e6d037b4e3335bae0986
- TRC20 USDT TG83ASaHPCi9TDTjKCfMbmyfjBvme9XjqC
- Text me on Discord or Telegram for PayPal or any other crypto

# Bot configuration
## Commands
- `stop` Write this into the console and press enter. The bot will stop after all ongoing battles are finished.

## General Settings

- `PRIORITIZE_QUEST=true` Disable/Enable quest priority.

- `SLEEP_BETWEEN_BATTLES=5` Sleep time in minutes before the bot will fight with an account again. You can set it to 0 to fight without any break.

- `ECR_THRESHOLD=75` If your energy capture rate goes below this the bot will stop fighting with this account until it's above again. Set to 0 to disable.

- `CLAIM_SEASON_REWARD=false` Disable/Enable season reward claiming.

- `CLAIM_QUEST_REWARD=false` Disable/Enable quest reward claiming.

- `DONT_CLAIM_QUEST_NEAR_HIGHER_LEAGUE=true` Example: If you are almost bronze 1 and have enough power for it, the bot won't claim the quest reward until you are bronze 1.

- `ADVANCE_LEAGUE=true` Disable/Enable the bot advancing to silver and above.

- `REQUEST_NEW_QUEST=earth` Quests the bot isn't good at and where you want it to request a new one. Seperate by comma, possible options are: fire,water,earth,life,death,dragon

## Lightning Mode Settings

- `USE_LIGHTNING_MODE=true` Disable/Enable lightning mode. Lightning mode has 90% less API requests to the splinterlands API which means you can run a lot of accounts in parallel without getting IP banned. It's also super fast and barely takes any CPU and RAM resources.

- `THREADS=1` Number of threads (= number of accounts fighting in parallel).

- `SHOW_BATTLE_RESULTS=true` Disable/enable showing battle results in console.

## Advanced Settings
- `SHOW_API_RESPONSE=true` Disable/Enable showing the team picked by the battle API in console.

- `DEBUG=false` Disable/Enable showing more log in console. I don't recommend to enable this unless you have been asked to.

- `WRITE_LOG_TO_FILE=false` Disable/Enable writing the console log to a log.txt file in the bot main directory.

- `DISABLE_CONSOLE_COLORS=false` Disable/Enable colors in console.

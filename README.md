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
- And much more, see [Bot configuration](https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2/blob/master/README.md#bot-configuration)
- Any suggestions?

## How to install (Windows)
- [Youtube Tutorial](https://www.youtube.com/watch?v=wVHL94ZH5r8)
- Download the bot from the [Releases Page](https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2/releases) and extract it
- Install https://dotnet.microsoft.com/download/dotnet/5.0/runtime (for console applications)
- Install Google Chrome
- Download the correct win32 chromedriver for your Chrome version (if you have chrome 96 download chromedriver 96): https://chromedriver.chromium.org/downloads 
- Put the chromedriver.exe (inside the .zip) in bot main folder
- Create config.txt and accounts.txt in config folder - see config-example.txt and accounts-example.txt
- Start the bot by double clicking on Ultimate Splinterlands Bot V2.exe in main folder

## How to install (Linux / MacOS)
- [Youtube Tutorial](https://www.youtube.com/watch?v=kTS0FdAei7c)
- [Chrome download](https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb)
- [Chromedriver download](https://chromedriver.chromium.org/downloads)
- Text instructions coming soon, please ask for instructions on discord or Telegram

[Discord](https://discord.gg/hwSr7KNGs9)

[Telegram](https://t.me/ultimatesplinterlandsbot) 

# Donations

In case you want to donate to me for creating this bot, I would be very happy!

- DEC/SPS into the game to the player **pcjones** 
- Bitcoin 3KU85k1HFTqCC4geQz3XUFk84R6uekuzD8
- Ethereum 0xcFE8c78F07e0190EBdD9077cF9d9E3A8DCED8d91 
- WAX to account **lshru.wam** (please copy the name)
- BUSD/USDT/BNB etc (Binance Smart Chain) 0x951844e1525bf37f36d7e6d037b4e3335bae0986
- TRC20 USDT TG83ASaHPCi9TDTjKCfMbmyfjBvme9XjqC
- Text me on Discord or Telegram for PayPal or any other crypto

## Bot configuration:
Configuration with default values:

# General Settings

- `PRIORITIZE_QUEST=true` Disable/Enable quest priority

- `SLEEP_BETWEEN_BATTLES=5` Sleep time in minutes before the bot will fight with an account again. You can set it to 0 to fight without any break.

- `ECR_THRESHOLD=75` If your energy capture rate goes below this the bot will stop fighting with this account until it's above again. Set to 0 to disable

- `CLAIM_SEASON_REWARD=false` Disable/Enable season reward claiming

- `CLAIM_QUEST_REWARD=false` Disable/Enable quest reward claiming

- `DONT_CLAIM_QUEST_NEAR_HIGHER_LEAGUE=true` Example: If you are almost bronze 1 and have enough power for it, the bot won't claim the quest reward until you are bronze 1

- `HEADLESS=true` Disable/Enable headless("invisible") browser (e.g. to see where the bot fails)

- `KEEP_BROWSER_OPEN=true` Disable/Enable keeping the browser instances open after fighting. Recommended to have it on true to avoid having each account to login for each fight. Disable if CPU/Ram usage is too high (check in task manager)

- `LOGIN_VIA_EMAIL=false` Disable/Enable login via e-mail adress. See below for further explanation

- `EMAIL=account1@email.com,account2@email.com,account3@email.com` Your login e-mails, each account seperated by comma. Ignore line if `LOGIN_VIA_EMAIL` is `false`

- `ACCUSERNAME=username1,username2,username3` Your login usernames, each account seperated by comma. **Even if you login via email you have to also set the usernames!**

- `PASSWORD=password1,password2,password3` Your login passwords/posting keys. Use password if you login via email, **use the posting key if you login via username**

- `USE_API=true` Enable/Disable the team selection API. If disabled the bot will play the most played cards from local newHistory.json file. **Experimental - set to false if you lose a lot**

- `API_URL=` Ignore/Don't change unless you have the private API from the original bot

- `USE_CLASSIC_BOT_PRIVATE_API=false` Set to false unless you have the private API from the original bot

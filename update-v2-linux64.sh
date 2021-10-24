#!/bin/

# Linux V2 bot directory, change the path if your bot is in different directory
BOTHPATH = /root/linux-x64

# Temporary directory for the latest release
cd /tmp
## mkdir temp
## cd temp

# Download Latest Release
wget https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2/releases/latest/download/linux-x64.zip
unzip linux-x64.zip
rm linux-x64.zip


# Sync New release but exclude the config directory. Add -n if you want to dry run the sync to check if something goes wrong.(-aPn)
rsync -aP --exclude "config" /tmp/linux-x64 $BOTHPATH

sudo rm -r /tmp/linux-x64

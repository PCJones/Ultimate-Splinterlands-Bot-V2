#!/bin/sh
sudo apt-get update && sudo apt-get upgrade -y

wget https://github.com/PCJones/Ultimate-Splinterlands-Bot-V2/releases/latest/download/linux-x64.zip
unzip linux-x64.zip
rm linux-x64.zip

cd linux-x64

sudo apt-get update; \
  sudo apt-get install -y apt-transport-https && \
  sudo apt-get update && \
  sudo apt-get install -y aspnetcore-runtime-5.0

wget https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
dpkg -i google-chrome-stable_current_amd64.deb
wget https://chromedriver.storage.googleapis.com/94.0.4606.61/chromedriver_linux64.zip
unzip chromedriver_linux64.zip
rm chromedriver_linux64.zip

touch chromedriver.exe
chmod +x Ultimate\ Splinterlands\ Bot\ V2


echo "./Ultimate\ Splinterlands\ Bot\ V2" to start
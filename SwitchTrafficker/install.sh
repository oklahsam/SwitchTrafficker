#!/bin/sh

echo "This script will install SwitchTrafficker as a service and any prerequisites";
echo "Please make sure you have configured SwitchTrafficker.conf before running";

read -p "Are you sure you want to install now? [y/N] " yn;

if [ "y" != "$yn" ]; then
    echo "Exiting....";
    exit 0;
fi

wget https://packages.microsoft.com/config/ubuntu/21.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb;
dpkg -i packages-microsoft-prod.deb;
rm ./packages-microsoft-prod.deb;
apt-get update;
apt-get install -y apt-transport-https dotnet-sdk-6.0;
cp -r ../SwitchTrafficker/ /srv/SwitchTrafficker/;

systemctl enable /srv/SwitchTrafficker/SwitchTrafficker.service --now
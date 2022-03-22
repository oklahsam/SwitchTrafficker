wget https://packages.microsoft.com/config/ubuntu/21.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb;
dpkg -i packages-microsoft-prod.deb;

rm ./packages-microsoft-prod.deb;

apt-get update;
apt-get install -y apt-transport-https dotnet-sdk-6.0;

cp -r ../SwitchTrafficker/ /srv/SwitchTrafficker/;

cd /srv/SwitchTrafficker/;
$SHELL
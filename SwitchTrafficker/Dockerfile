FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy

RUN mkdir /mnt/SwitchTrafficker
COPY SwitchTrafficker/* /mnt/SwitchTrafficker/
WORKDIR /mnt/SwitchTrafficker

ENTRYPOINT ["dotnet", "/mnt/SwitchTrafficker/SwitchTrafficker.dll"]
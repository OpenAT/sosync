# sosync
Synchronizes data between **FundraisingStudio (FS)** and **FundraisingStudio Online (FSO)**.

## Architecture
- **sosync** is an *ASP.NET Core* Application, written in *C#*
- It runs a *Kestrel* webserver to provite a *REST*ful API
- The API is used to
  - start and stop a single background thread
  - add new sync jobs
- The background thread processes the sync jobs

## Setup
### .NET Core SDK on Ubuntu 14.04 trusty:
```
sudo sh -c 'echo "deb [arch=amd64] https://apt-mo.trafficmanager.net/repos/dotnet-release/ trusty main" > /etc/apt/sources.list.d/dotnetdev.list'
sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 417A0893
sudo apt-get update

sudo apt-get install dotnet-dev-1.0.4
```
Source: [https://www.microsoft.com/net/core#linuxubuntu](url) (includes instructions for other systems)

### Building the source
- Download the repository
- Change the working directory to the repository
- Run the following commands

```
dotnet restore
dotnet publish -c Release -o destination_path
```


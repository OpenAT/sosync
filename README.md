# sosync
Synchronizes data between **FundraisingStudio (FS)** and **FundraisingStudio Online (FSO)**.

## Architecture
- **sosync** is an *ASP.NET Core* Application, written in *C#*
- It is supposed to run as a linux service
- It runs a self contained *Kestrel* webserver to provite a *REST*ful API
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
dotnet publish -c Release -o ./../bin/Publish
```
While developing:
- In Visual Studio use the publish command, the directory is pre-configured
- In Linux you can use the shell script **publish.sh**
- Don't forget to **push** after publish, the publish binaries are source controlled.

### Additional requirements
Before the service can be started, ensure the following (replace **dadi** with the actual instance name):
- In the application directory create an INI file, **dadi_sosync.ini**
- Setup a pgSQL database
  - Setup DNS for the server
  - Create a database for the instance, e.g.: **dadi**
  - create a user for the database with create table permissions
- Optional: Create the directory **/var/log/sosync/dadi**, ensure the user running the service for this instance has access to (and only to) it's instance folder.

#### Example INI file:
```
Logging:IncludeScopes=false

# Log levels: None, Trace, Debug, Information, Warning, Error, Critical
Logging:LogLevel:Default=Information

sosync_user=theuser
sosync_pass=thepass
```

### Running the application
Change the working directory to the application directory. After that the following command can be used to start and run the application:
```
dotnet sosync.dll --instance dadi --server.urls="http://localhost:5000"
```
- Replace **dadi** with the proper instance name.
- Replace the URL with the proper server name and port


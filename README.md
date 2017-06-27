# sosync
Synchronizes data between **FundraisingStudio (FS)** and **FundraisingStudio Online (FSO)**.

## Architecture
### Overview
- **sosync** is an *ASP.NET Core* Application, written in *C#*
- It is supposed to run as a linux service
- It runs a self contained *Kestrel* webserver to provite a *REST*ful API
- The API returns strings for simple results, and XML or json for complex results (set desired "Accept" header)
- The background thread processes the sync jobs. There is a maximum of one background thread at any given time.

### The API
Depending on the "**Accept**" header, most routes return either a **json** object with the fields **state** and **stateDescription**, or an **XML** object with the elements **State** and **StateDescription** (be aware of the difference in letter case between XML and json).
- **/state** returns the current state of the background thread
  - 0: Stopped - background thread is idle
  - 1: Running - ongoing synchronization
  - 2: RunningRestartRequested - ongoing synchronization. A restart is scheduled
  - 3: Stopping - background thread will finish the current job, then stop
  - 4: Error - The last execution of the background thread caused an error
- **/state/start** starts the background thread. Subsequent calls set a restart flag
  - 0: AlreadyRunningRestartRequested - background thread already running. A restart was requested
  - 1: Started - background thread was started
  - 2: ShutdownInProgress - cannot start the background thread. The service is shutting down
- **/state/stop** gracefully stops the background thread. Subsequent calls have no effect
  - 0: StopAlreadyRequested - the background has already received a stop request
  - 1: StopRequested - the background thread was asked to stop

- **/version** returns the full length git commit id as a string

- **/job** takes **GET** parameters to create a new sync job
  - job_date, should be a UTC date and time
  - source_system, **fs** or **fso**
  - source_model, the model name
  - source_record_id, the ID in the source system

## Setup
### .NET Core SDK on Ubuntu 14.04 trusty main:
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

#### Example INI configuration:
```
[sosync]
port = 5050
instance = inst

# Database
db_host = localhost
db_port = 5432
db_name = inst
db_user = theuser1
db_user_pw = thepass15

# Logging
log_file = /var/log/sosync/inst/inst.log
log_level = Information

# Fundraising Studio
studio_mssql_host = mssql.debug.datadialog.net
studio_sosync_user = theuser2
studio_sosync_pw = thepass16

# FS-Online
online_host = debug.datadialog.net
online_sosync_user = theuser3
online_sosync_pw = thepass17
```

### Running the application
Change the working directory to the application directory. After that the following command can be used to start and run the application:
```
dotnet sosync.dll --conf /path/dadi.ini
```
- Replace **/path/dadi.ini** with the proper configuration file name
- All values in the INI can also be specified as parameter
- Parameters override INI settings

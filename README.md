# sosync
Synchronizes data between **FundraisingStudio (FS)** and **FundraisingStudio Online (FSO)**.

## Architecture
### Overview
- **sosync** is an *ASP.NET Core* Application, written in *C#*
- It is supposed to run as a linux service
- It runs a self contained *Kestrel* webserver to provite a *REST*ful API
  - By default, localhost and all IPv4 addresses will be listened on
  - INI configuration should override this behaviour, but that is **not implemented yet**
- The API returns strings for simple results, and XML or json for complex results (set desired "Accept" header)
- The background thread processes the sync jobs. There is a maximum of one background thread at any given time.

### Project structure
- **Common** common interfaces and enumerations
- **Data** data access layer (sosync only)
- **Odoo** Odoo API implementation, utilizes the XmlRpc library
- **Syncer** the actual syncer and **sync flows**
  - Actual sync flows can be found in [Syncer/Flows](https://github.com/OpenAT/sosync/tree/v2/Syncer/Flows)
  - References **Odoo** for FSO access
  - References **dadi-data** (DaDi-Nuget) for FS access
- **WebSosync** Kestrel webserver providing the API and housing the syncer
- **XmlRpc** makeshift XML RPC library

### The API
Most routes return either a **json**/**XML** object (depending on the sent "**Accept**" header) or plain text.
- **/service/status** returns information about all background jobs
  - job_worker: The thread that handles data synchronization
  - protocol_worker: The thread that transfers sync jobs to Odoo where they can be viewed
- **/service/version** returns the full length git commit id as plain text.
- **/job/create** takes **GET** parameters to create a new sync job
  - job_date, should be a UTC date and time
  - source_system, **fs** or **fso**
  - source_model, the model name
  - source_record_id, the ID in the source system
- **/job/info/{id}** returns the full job row
- **/job/list** returns the full sync table

## Setup
### .NET Core SDK on Ubuntu 14.04 trusty main:
```
sudo sh -c 'echo "deb [arch=amd64] https://apt-mo.trafficmanager.net/repos/dotnet-release/ trusty main" > /etc/apt/sources.list.d/dotnetdev.list'
sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 417A0893
sudo apt-get update

sudo apt-get install dotnet-dev-1.0.4
```
Source: [dotnet core site](https://www.microsoft.com/net/core#linuxubuntu) (includes instructions for other systems)

### Building the source
- Download the repository
- Change the working directory to the repository
- Run the following commands

```
dotnet restore
dotnet publish -c Release -o ./../bin/Publish
```
While developing:
- In both Linux and Windows, the shell script **publish.sh** can be used to prepare publish binaries
- In Visual Studio use the publish command, the directory is pre-configured
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
throttle_ms = 0

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

If throttle_ms equals 0, throttling will be omitted totally. Otherwise, throttle_ms represents the minimum time
a sync job will occupy. Meaning, if a sync job finishes in a time larger than throttle_ms, no throttling will
occur at all, if a sync job finishes faster, it will sleep for the remaining time.

### Running the application
Change the working directory to the application directory. After that the following command can be used to start and run the application:
```
dotnet sosync.dll --conf /path/dadi.ini
```
- Replace **/path/dadi.ini** with the proper configuration file name
- All values in the INI can also be specified as parameter
- Parameters override INI settings

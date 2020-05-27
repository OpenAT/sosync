# Sync Flows

Contains the actual sync flows doing the data comparison and transformations
between FSOnline and FS.

- **_SyncFlow.cs** base class for all flows. But it is not used directly. It Uses the
  [Template Method Pattern](https://en.wikipedia.org/wiki/Template_method_pattern)
  to ensure the basic flow is always the same. Following base classes inherit from SyncFlow,
  these are the base classes that you can inherit for creating a new Sync Flow:
    - _ReplicateSyncFlow base class for normal flows
	- _MergeSyncFlow base class for flows that merge data rows (e.g. Person doublets)
	- _DeleteSyncFlow base class for flows that delete models
  - See the **public void Start(SyncJob job)** method for the flow logic
- Special notes to **PartnerFlow** (converts **res.partner** and **dbo.Person**): due to model differences, this flow also deals with
  - dbo.PersonAdresse
  - dbo.PersonTelefon
  - dbo.PersonEmail

# Finding Sync Flows
Each sync flow, has the involved table names or model names as attribute on the class.

For example:
- Searching for "**dbo.Person**" or "**res.partner**" will bring up the file **PartnerFlow.cs**
  that's where all the Transformation etc. is happening

# Flows not using Simple-Transform
The following flows do not yet use the generalized **SimpleTransformTo...** Methods yet.
These flows should be converted at some point!

- EmailTemplateFlow
- GroupsFlow (currently commented out)
- UsersFlow (currently commented out)

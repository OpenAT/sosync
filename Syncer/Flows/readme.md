# Sync Flows

Contains the actual sync flows doing the data comparison and transformations
between FSOnline and FS.

- **_SyncFlow.cs** contains the base class for all flows. Defines the flow
  without any specifics about concrete models.
- **CompanyFlow** converts between **res.company** and **xBPKAccount** 
- **PartnerFlow** converts between **res.partner** and **dbo.Person (including dbo.PersonAddress, etc.)**

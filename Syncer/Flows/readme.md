# Sync Flows

Contains the actual sync flows doing the data comparison and transformations
between FSOnline and FS.

- **_SyncFlow.cs** base class for all flows. Uses the [Template Method Pattern](https://en.wikipedia.org/wiki/Template_method_pattern)
  to ensure the basic flow is always the same.
- **CompanyFlow** converts between **res.company** and **xBPKAccount** 
- **PartnerFlow** converts between **res.partner** and **dbo.Person (including dbo.PersonAddress, etc.)**

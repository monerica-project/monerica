# Monerica

A Directory For A Monero Circular Economy

[Submit your link to Monerica here!](https://monerica.com/submit)

--------------

### Donations

You can donate to help with hosting and admin costs by sending Monero to: `8BzHMDw2UaXNpCZM9wxABXW3qAKMxM2WxDGuDWSf5x5v7t1PdWdMfdLCzdtK8Eb9C5ZHcEHNR85bcbWhuK8SLCH46Pvy71q`

![Donate To Monerica](https://user-images.githubusercontent.com/108239499/232602369-831b5fea-de63-4de2-8985-0aa8bb304111.png)

--------------
### About

Monerica is a directory of websites and services that accept Monero as payment or relate to Monero in some way.
It is a community project that is open to contributions from anyone. 
The goal is to create a directory of websites and services facilitating a circular economy for Monero.

### Contributing

The best way to contribute to Monerica is to help manage the data in the directory.
This can be done by [submitting new links](https://monerica.com/submit) or [editing existing links](https://monerica.com/submission/findexisting).

If you would like to contribute to the codebase, please fork the repository and submit a pull request.

The Monerica website is built using C# and ASP.NET Core MVC. 
It is possible to run the website on Windows, Linux, or Mac but it is recommended to use Windows for development with Visual Studio.
The database is a SQL Server database and the code is written using Entity Framework Core.

### Application Settings

The application settings are stored in the appsettings.json file. 
You can use the appsettings.Development.json file to override settings for development.
Copy the appsettings.template.json file in the project you want to run and rename it to appsettings.json.
From there you put in your own values for the settings.

At this time you will need to use a SQL Server database.

### Database Migrations

The database is managed using Entity Framework Core.
To create a new migration, run the following command from the Monerica.Web directory:
`dotnet ef migrations add MigrationName --context ApplicationDbContext --project DirectoryManager.Data\DirectoryManager.Data.csproj`

Then run:
`dotnet ef database update --context ApplicationDbContext --project DirectoryManager.Data\DirectoryManager.Data.csproj`

### Application Structure

The application is split into 3 projects:
- DirectoryManager.Web - This is the main web application that contains the controllers and views.
- DirectoryManager.Data - This is the data access layer that contains the database context and migrations.
- DirectoryManager.Console - This is used to perform local tasks such as checking which links are broken.

### Features

DirectoryManager is capable of the following:
- Adding new links to the directory
- Editing existing links in the directory
- Deleting links from the directory
- Viewing all links in the directory
- Creating the corresponding catagory and sub category when adding a new link
- Auditing the links

### Deployment

You can deploy an instance of the application, which will upgrade the database, using the CI.bat file. Run this in a PowerShell .ps1 file and set your settings:

```
$MsDeployLocation            = "https://YOURDOMAIN.com:8172"
$webAppHost                  = "YOURDOMAIN.com"
$contentPathDes              = "C:\sites\YOURDOMAIN.com\"
$msDeployUserName            = 'YOURUSERNAME';
$msDeployPassword            = 'YOURPASSWORD';
$dbConnectionString          = 'YOUR_SQL_SERVER_CONNECTION_STRING';

cd "PATH_TO_BAT_FILE"

.\ci.bat DeployWebApp  -properties "@{'MsDeployLocation'='$MsDeployLocation';'webAppHost'='$webAppHost';'contentPathDes'='$contentPathDes';'msDeployUserName'='$msDeployUserName';'msDeployPassword'='$msDeployPassword';'dbConnectionString'='$dbConnectionString';}"

write-host "done"
```




####################################################
# WebPagePub CI Tool
#
# Example use: .\CI.bat TaskName -properties "@{'Key'='Value'}"
####################################################


# External scripts
$CIRoot               = $PSScriptRoot
$PowerShellScriptPath = "$CIRoot\Scripts\"
$CIOutputDirectory    = "$CIRoot\..\CI-Output"

Include (Join-Path $PowerShellScriptPath "ApplicationHelperFunctions.ps1")
Include (Join-Path $PowerShellScriptPath "AzureHelperFunctions.ps1")
Include (Join-Path $PowerShellScriptPath "BuildHelperFunctions.ps1")
Include (Join-Path $PowerShellScriptPath "GeneralHelperFunctions.ps1")
Include (Join-Path $PowerShellScriptPath "AzureHelperFunctions.ps1")
Include (Join-Path $PowerShellScriptPath "GitHelperFunctions.ps1")

# Define CLI input properties and defaults
properties {

   # Build
   $BuildConfiguration          = "release"
   $DotNetRunTime               = "win7-x64"
   $DotNetFramework             = "net7.0"
   $msDeploy                    = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"    
   
   # Project paths
   $databaseProjectSourcePath   = "..\src\DirectoryManager\DirectoryManager.Data"
   $webProjectSourcePath        = "..\src\DirectoryManager\DirectoryManager.Web"
   $testProjectSourcePath       = "..\src\DirectoryManager\DirectoryManager.sln"
   $compileSourcePath           = "..\src\DirectoryManager.Web\bin\output"

   $WebAppSettings              = "..\src\DirectoryManager\DirectoryManager.Web\appsettings.json"
   $DatabaseAppSettings         = "..\src\DirectoryManager\DirectoryManager.Data\appsettings.json"

   # Credentials
   $MsDeployLocation            = ""
   $webAppHost                  = ""
   $contentPathDes              = ""
   $msDeployUserName            = ""
   $msDeployPassword            = ""
   $dbConnectionString          = ""
   $NeutrinoApiUserId           = ""
   $NeutrinoApiApiKey           = ""
   $deploymentUserName          = ""
   $deploymentUserPassword      = ""
   $AzureSubscriptionId         = ""

   # Azure
   $AppShortName                = "" # 3 chars max
   $DataCenter                  = "West US"
   $EnvironmentName             = "prod"

   # Git   
   $RemoteOriginName            = "origin"
   $MasterBranchName            = "master"

   # General
   $DefaultBrowser              = "chrome"

   # Application Resources    
   $portalAppLocation                 = "West US"    
   $templateFile                      = "$CIRoot\app-templates\azuredeploy.json"
   $DefaultTemplateParameterFile      = "$CIRoot\app-templates\azuredeploy.parameters.json"
   $EnvironmentTemplateParameterFile  = "$CIRoot\app-templates\azuredeploy.parameters.current.json"
   $storageContainerName              = "packages"
   $customDomain                      = ""

   # DNS Resources
   $dnsTemplateFile                     = "$CIRoot\app-templates\azuredeploy-dns.json"
   $defaultDnsTemplateParameterFile     = "$CIRoot\app-templates\azuredeploy-dns.parameters.json"
   $EnvironmentDnsTemplateParameterFile = "$CIRoot\app-templates\azuredeploy-dns.parameters.current.json"
}

task default # required task

##############
# Compilation
##############

task -name create {
 
$cert = New-SelfSignedCertificate -CertStoreLocation "cert:\CurrentUser\My" `
  -Subject "CN=exampleappScriptCert" `
  -KeySpec KeyExchange
$keyValue = [System.Convert]::ToBase64String($cert.GetRawCertData())

$sp = New-AzureRMADServicePrincipal -DisplayName exampleapp `
  -CertValue $keyValue `
  -EndDate $cert.NotAfter `
  -StartDate $cert.NotBefore
Sleep 20
New-AzureRmRoleAssignment -RoleDefinitionName Contributor -ServicePrincipalName $sp.ApplicationId

}




task -name BuildProject -description "Build Project"  -action { 
   
     exec {
     
        $webProjectPath = Resolve-Path -Path ("$CIRoot\$webProjectSourcePath")

        cd $webProjectPath

        dotnet restore
    }
}

task -name RestorePackages -description "Restore Packages"  -action { 
   
     exec {

        $compileSourcePath = ("$CIRoot\$compileSourcePath")
        
        if (Test-Path -Path $compileSourcePath){
            Write-Host "Deleting files at: '$compileSourcePath'... " -NoNewline
            
            Remove-Item $compileSourcePath -Force -Recurse

            Write-Host "done." -NoNewline
            Write-host
        }
                            
        $webProjectPath = Resolve-Path -Path ("$CIRoot\$webProjectSourcePath")

        cd $webProjectPath

        
        dotnet msbuild /t:Restore /p:Configuration=$BuildConfiguration

    }
}

task -name CreatePackage {

    exec {

        if (!(Test-Path -Path ("$CIRoot\$compileSourcePath")))
        {
            New-Item -Path ("$CIRoot\$compileSourcePath") -ItemType directory
        }

        $compileSourcePath = Resolve-Path -Path ("$CIRoot\$compileSourcePath")

        $webProjectPath = Resolve-Path -Path ("$CIRoot\$webProjectSourcePath")

        cd $webProjectPath

        Write-Host "Packaging..."
    
        & dotnet publish `
                    --framework $DotNetFramework `
                    --output $compileSourcePath `
                    --configuration $BuildConfiguration `
                    --runtime $DotNetRunTime
    }
}

task -name RunUnitTests {

    exec {
        $path = Resolve-Path -Path $testProjectSourcePath
        Write-Host "Test project: $path"
        dotnet test $path
    }
}

task -name SetConfigs {


    $WebAppSettings = "..\src\WebPagePub.WebApp\appsettings.json"
    Set-FileSettings -fileLocation $WebAppSettings

    $DatabaseAppSettings = "..\src\WebPagePub.Data\appsettings.json"
    Set-FileSettings -fileLocation $DatabaseAppSettings

}

############
# Deployment
############

task -name CustomServerDeploy `
     -depends DeployWebApp {


}

task -name SyncWebFiles {

    exec {

        $webconfigPath = $contentPathDes + "web.config"
        $deployIisAppPath = $webAppHost
        
        Write-Host "Deleting config..."
        
        & $msDeploy `
            -verb:delete `
            -allowUntrusted:true `
            -dest:contentPath=$webconfigPath,computername=$MsDeployLocation/MsDeploy.axd?site=$deployIisAppPath,username=$msDeployUserName,password=$msDeployPassword,authtype=basic
        Write-Host "done."

        $compileSourcePath = Resolve-Path -Path ("$CIRoot\$compileSourcePath")

        Write-Host "-------------"
        Write-Host "Deploying..."

        & $msDeploy `
            -verb:sync `
            -source:contentPath=$compileSourcePath `
            -allowUntrusted:true `
            -dest:contentPath=$contentPathDes,computername=$MsDeployLocation/MsDeploy.axd?site=$deployIisAppPath,username=$msDeployUserName,password=$msDeployPassword,authtype=basic
        Write-Host "done."
    }
}

task -name MigrateDB -description "Runs migration of database"  -action { 
   
     exec {
      
       $databaseProjectSourcePath = Resolve-Path -Path ("$CIRoot\$databaseProjectSourcePath")
       cd $databaseProjectSourcePath
       Write-Host $databaseProjectSourcePath

       dotnet ef database update --verbose
    }
}

task -name DeployWebApp -depends SetConfigs, RestorePackages, BuildProject, RunUnitTests, CreatePackage, MigrateDB, SyncWebFiles -action {

    exec {

        $url = "http://$webAppHost"
        Write-Host "Deployment completed, requesting page '$url'..." -NoNewline 
        
        $response = Invoke-WebRequest -Uri $url

        if ($response.StatusCode -eq 200)
        {
            Write-Host "done." -NoNewline
            Write-Host
                
            Write-Host "COMPLETE!"
        }
        else 
        {
            Write-Error "Status code was: " + $response.StatusCode
        }
    }
}


##############
# Azure Tasks
##############

task -name CreateResouceGroups `
     -depends Authenticate {

    exec {
        $dnsResourceGroupName = Get-GetDnsResourceGroupName -AppShortName $AppShortName -DataCenter $DataCenter
        $appResourceGroupName = Get-GetAppResourceGroupName -AppShortName $AppShortName -EnvironmentName $EnvironmentName -DataCenter $DataCenter       

        Create-ResourceGroupIfNotExists `
                        -AzureResourceGroupName $appResourceGroupName `
                        -AzureDataCenter $DataCenter


        $appStorageAccountName = Get-GetAppStorageAccountName -AppShortName $AppShortName -EnvironmentName $EnvironmentName -DataCenter $DataCenter 

        if (!(Has-AzureStorageAccount -StorageAccountName $appStorageAccountName))
        {
            write-host "creating storage account $appStorageAccountName..."
            
            New-AzureRmStorageAccount `
                -ResourceGroupName $appResourceGroupName `
                -Name $appStorageAccountName `
                -Location $dataCenter `
                -Type $StorageAccountType | Out-Null
        }

        $ServiceBusNamespace = Get-ServiceBusName -AppShortName $AppShortName -EnvironmentName $EnvironmentName
        $result = (Has-AzureServiceBusNamespace -ServiceBusNamespace $ServiceBusNamespace -ResourceGroupName $appResourceGroupName)
 
        if (![System.Convert]::ToBoolean($result))
        {
            Write-Host "Creating Service Bus $ServiceBusNamespace..."
        
            New-AzureRmServiceBusNamespace `
                -ResourceGroup $appResourceGroupName `
                -NamespaceName $ServiceBusNamespace `
                -Location $DataCenter `
                -SkuName "Standard" 
        }
        else 
        {
            Write-Host "Service Bus $ServiceBusNamespace exists"
        }
    }
}

task -name DeployDnsResources `
     -depends Authenticate, CreateResouceGroups {
    
    exec {
     
        $dnsResourceGroupName = Get-GetDnsResourceGroupName -AppShortName $AppShortName -DataCenter $DataCenter

        Write-Host "Testing template '$dnsTemplateFile' with parameters '$EnvironmentDnsTemplateParameterFile'..."

        $resultOfValidation = Test-AzureRmResourceGroupDeployment `
                                    -ResourceGroupName $dnsResourceGroupName `
                                    -TemplateFile $dnsTemplateFile `
                                    -TemplateParameterFile $EnvironmentDnsTemplateParameterFile 

        Write-Host "Validated"

        if ($resultOfValidation)
        {
           $resultOfValidation
           throw "Validation Failed!"
        }
        else 
        {
            Write-Host "valid template"
        }

        New-AzureRmResourceGroupDeployment `
            -Name $deploymentName `
            -ResourceGroupName $dnsResourceGroupName `
            -TemplateFile $dnsTemplateFile `
            -TemplateParameterFile $EnvironmentDnsTemplateParameterFile `
            -Verbose
    }
}

task -name ResourceGroupDeployment `
     -depends Authenticate, SetDeploymentParameters, CreateResouceGroups, DeployDnsResources {
     
     exec {
        $appStorageAccountName = Get-GetAppStorageAccountName -AppShortName $AppShortName -EnvironmentName $EnvironmentName -DataCenter $DataCenter 
        $appResourceGroupName = Get-GetAppResourceGroupName -AppShortName $AppShortName -EnvironmentName $EnvironmentName -DataCenter $DataCenter
             
        $key = (Get-AzureRmStorageAccountKey `
                                    -ResourceGroupName $appResourceGroupName `
                                    -Name $appStorageAccountName).Value[0]
 
        $storageCtx = New-AzureStorageContext -StorageAccountName $appStorageAccountName -StorageAccountKey $key
 
        $portalSitePackageUrl = New-AzureStorageBlobSASToken `
                            -Container $storageContainerName `
                            -Blob $PortalSitePackageName `
                            -Context $storageCtx `
                            -Permission r `
                            -ExpiryTime (Get-Date).AddHours(2.0) `
                            -FullUri  

        $bytes = [System.IO.File]::ReadAllBytes($pfxPath)
        $b64 = [System.Convert]::ToBase64String($bytes)
        $b64Secure = ConvertTo-SecureString $b64 –asplaintext –force 
  
        Write-Host "Testing template '$templateFile' with parameters '$EnvironmentTemplateParameterFile'..."
        $resultOfValidation = Test-AzureRmResourceGroupDeployment `
                                    -ResourceGroupName $appResourceGroupName `
                                    -TemplateFile $templateFile `
                                    -TemplateParameterFile $EnvironmentTemplateParameterFile `
                                    -portalSitePackageUrl $portalSitePackageUrl `
                                    -trafficSitePackageUrl $trafficSitePackageUrl `
                                    -pfxString $b64Secure
 
        Write-Host "Validated"
 
        if ($resultOfValidation)
        {
           $resultOfValidation
           throw "Validation Failed!"
        }
        else 
        {
            Write-Host "valid template"
        }
 
        Write-Host "Deploying..."
        New-AzureRmResourceGroupDeployment `
            -Name $deploymentName `
            -ResourceGroupName $appResourceGroupName `
            -TemplateFile $templateFile `
            -TemplateParameterFile $EnvironmentTemplateParameterFile `
            -portalSitePackageUrl $portalSitePackageUrl `
            -trafficSitePackageUrl $trafficSitePackageUrl `
            -pfxString $b64Secure `
            -Verbose
 
        $PortalSiteName = Get-PortalSiteName -AppShortName $AppShortName -EnvironmentName $EnvironmentName -DataCenter $dataCenter
        Set-WebsiteSslBinding -SiteName $PortalSiteName -ResourceGroupName $appResourceGroupName
    }
}

task -name Authenticate `
     -description "Authenticates against Azure" `
     -action {

     exec {
        if ([string]::IsNullOrWhiteSpace($deploymentUserName))
        {
            Write-Error "'deploymentUserName' not provided."
        }

        if ([string]::IsNullOrWhiteSpace($deploymentUserPassword))
        {
            Write-Error "'deploymentUserPassword' not provided."
        }

        write-host "Logging in..." -NoNewline
        $securePassword = ConvertTo-SecureString -String $deploymentUserPassword -AsPlainText -Force
        $cred = New-Object System.Management.Automation.PSCredential($deploymentUserName, $securePassword)
        Login-AzureRmAccount -Credential $cred -SubscriptionId $AzureSubscriptionId | Out-Null
        Write-Host "done." -NoNewline
        Write-Host ""

        #Select-AzureRmSubscription -SubscriptionId $AzureSubscriptionId
    }
}

task -name ViewDNSCnames `
-depends Authenticate `
-description "Shows all the DNS CNAMEs containing the current environment name." {    

    $dnsResourceGroupName = Get-GetDnsResourceGroupName -AppShortName $AppShortName -DataCenter $DataCenter

    $zone = Get-AzureRmDnsZone -Name "$customDomain" -ResourceGroupName $dnsResourceGroupName
    $dnsMatches = (Get-AzureRmDnsRecordSet  -RecordType CNAME -Zone $zone) # | where {$_.Name.Contains($environmentName) }    
    
    Write-host "Name Servers:"
    Write-Host $zone.NameServers
    Write-Host

    #if ($dnsMatches -eq $null)
    #{
    #    Write-Host "No DNS entries found for this environment."
    #    return
    #}    
    
    Write-Host "DNS CNAME entries on domain '$customDomain'" # containing '$environmentName':"
    Write-Host
    $FormatString = "{0,-45} {1,-1} {2,-8}"    
    $FormatString -f "Domain", "|", "Points To"
    Write-Host "------------------------------------------------------------------------------"

    foreach($entry in $dnsMatches) {
        $urlFormat = "https://{0}.$customDomain/"
        $FormatString -f ($urlFormat -f $entry.Name), "|", ("https://{0}/" -f $entry.Records[0])
        Write-Host
    }
}

task -name SetDeploymentParameters {

    Write-Warning "todo"

}

#######################################
# Git
#######################################
task GitPull `
    -alias pull `
    -description "Pulls source code from origin master branch name." {

    exec {

        Write-host "Displaying any modified files:"
        git status --short

        Write-Host "Pulling from source repository... "
        git pull $RemoteOriginName $MasterBranchName
    }
}

task GitPush `
    -alias push `
    -description "Pushes to current branch, preventing pushes to master branch name, creates new branch if not created remotely." `
    -action {

    exec {
        $gitConsoleOutput = Push-ToGit

        Write-Host $gitConsoleOutput
    }
}

task GitPullRequest `
    -alias pr `
    -description "When there are local commits which have not been pushed remotely, pushes them and opens browser to create pull request." {

    exec {

        $gitConsoleOutput = Push-ToGit

        [string]$pullRequestUrl = Get-GithubPullRequestUrl -GitConsoleOutput $gitConsoleOutput

        if ([string]::IsNullOrWhiteSpace($pullRequestUrl)) { return }
                    
        Write-Host "Opening Pull Request '$pullRequestUrl'" -ForegroundColor Magenta
        Start-Sleep -Seconds 2 # wait for files to be read by server first
        Start-Process $DefaultBrowser $pullRequestUrl
    }
}

task GitCreateBranch `
     -alias branch `
     -description "Creates branch from current branch using prompt." {

    exec  {
        $branchName = Read-Host "Enter branch name"

        $branchName = $branchName.Trim()

        $existingBranch = git branch --list $branchName

        if ($existingBranch)
        {
            git checkout $branchName
        }
        else 
        {
            git checkout -b $branchName
        }
    }
}

task GitListBranches `
     -alias branches `
     -description "Lists all the current local branches." {

    exec  {
        # TODO: implement https://github.com/thinkbeforecoding/PSCompletion for autocompeltion of branch name selection
        git branch
    }
}

task GitDeleteBranch `
    -alias deletebranch `
    -description "Deletes the current branch, locally and remotely then checkouts to main branch." {

    exec {
        
        $branchName = Get-CurrentBranchName

        $confirmation = Read-Host "Are you sure you want to delete the branch '$branchName' locally and remotely? (Y/N)"
        
        if ($confirmation.ToUpper() -ne 'Y') {
            "Branch '$branchName', unchanged."
            return
        }

        Write-Warning "Deleting '$branchName'..."
        git checkout $MasterBranchName
        git push origin --delete $branchName
        git branch -D $branchName
        Write-Warning "Deleted '$branchName'."
    }
}


task GitCommitAll `
    -alias com `
    -description "Adds all files to git stage, prompts for commits message and creates commit." {

    exec {
        
        git status --short
        $commitMessage = Read-Host "Enter commit message"
        git add -A
        git commit -m $commitMessage
    }
}


##########################
# Other
##########################

task -name RenameFiles -description "Renames files in a directory" {

    $path = "c:\temp"
    $fileNamSubstringeToRemove = "-min"

    Get-ChildItem  -Path $path | Rename-Item -NewName { $_.Name -replace $fileNamSubstringeToRemove, '' }
}

##########################
# psake functions
##########################
FormatTaskName {
   param($taskName)
  
   write-host "----- Task: $taskName -----"   -foregroundcolor Cyan
}

TaskSetup {

    $time = Get-UtcDate
       
    Write-Host "Begin: $time "  -ForegroundColor DarkGray
}

TaskTearDown {

    Write-Host "----------"
}
 
##########################
# Azure Helper Functions
##########################

function Create-TableIfNotExists{    
    Param($TableName, 
          $StorageContext)

    Get-AzureStorageTable `
        -Name $TableName `
        -Context $StorageContext `
        -ErrorVariable ev `
        -ErrorAction SilentlyContinue | Out-Null
    
    if ($ev) {
        Write-Host "Creating table '$TableName'... " -NoNewline
        New-AzureStorageTable `
           –Name $TableName `
           –Context $StorageContext | Out-Null
        Write-Host "done." -NoNewline
        Write-Host ""
    }
    else {
        Write-Host "Table '$TableName' already exists."
    } 
}


function Create-AzureRmADApplicationIfNotExists {
    Param($DisplayName, $AppHomePage, $AppIdentifierUris)

    $appDisplayName = Get-AzureRmADApplication | select -Expand DisplayName
     
    if (!$appDisplayName.Contains($DisplayName))
    {
        Write-Host "Creating $DisplayName..."
        New-AzureRmADApplication -DisplayName $DisplayName -HomePage $AppHomePage -IdentifierUris $AppIdentifierUris
    }
}

function Get-AzureRmADApplicationAppId {
    Param($DisplayName)

    $AppId = Get-AzureRmADApplication -DisplayName $DisplayName | select -Expand ApplicationId

    $AppId
}

function Get-AzureRmADApplicationObjectId {
    Param($DisplayName)

    $ObjectId = Get-AzureRmADApplication -DisplayName $DisplayName | select -Expand ObjectId

    $ObjectId
}

function Has-AzureResourceGroup {
    Param([Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)
    
    Write-Host "Checking for $ResourceGroupName..."
    $ResourceGroupNames = (Get-AzureRmResourceGroup | select -Expand ResourceGroupName)

    $hasAccount = $false
    
    if($ResourceGroupNames -ne $null)
    {
        $hasAccount = $ResourceGroupNames.Contains($ResourceGroupName)
    }

    return [System.Convert]::ToBoolean($hasAccount)
}
 

function Has-AzureStorageAccount {
    Param([Parameter(Mandatory=$true)]
          [string]$StorageAccountName)

    write-host "checking for storage account '$StorageAccountName'..."

    $hasAccount = [System.Convert]::ToBoolean((Test-AzureName -Storage $StorageAccountName))
  
    $hasAccount    
}

function Get-SQLConnectionString {
    Param([Parameter(Mandatory=$true)]
          [string]$DatabaseServerName,
          [Parameter(Mandatory=$true)]
          [string]$DatabaseName,
          [Parameter(Mandatory=$true)]
          [string]$SqlLoginUsername,
          [Parameter(Mandatory=$true)]
          [string]$SqlLoginPassword)

    "Server=tcp:" + 
    $DatabaseServerName + 
    ".database.windows.net" + 
    ",1433;Initial Catalog=" + 
    $DatabaseName + 
    ";Persist Security Info=False;User ID=$SqlLoginUsername@$DatabaseServerName;Password=$SqlLoginPassword" + 
    ";MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
}


  
function Get-StorageAccountConnectionString {
    Param([Parameter(Mandatory=$true)]
          [string]$StorageAccountName,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName )

    $key = (Get-AzureRmStorageAccountKey `
                                -ResourceGroupName $ResourceGroupName `
                                -Name $storageAccountName).Value[0]

    "DefaultEndpointsProtocol=https;AccountName=$storageAccountName;AccountKey=$key;EndpointSuffix=core.windows.net";
}


function Has-AzureServiceBusNamespace {
    Param([Parameter(Mandatory=$true)]
          [string]$ServiceBusNamespace,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)
 
    Write-host "Checking for $ServiceBusNamespace..."

    $result = Get-AzureRmResource `
                    -ResourceType Microsoft.ServiceBus\namespaces `
                    -ResourceName $ServiceBusNamespace `
                    -ResourceGroupName $ResourceGroupName `
                    -ApiVersion 2017-04-01

    write-host $result

    if ($result -eq $null)
    {
        return [System.Convert]::ToBoolean($False) 
    }
    else 
    {
        return [System.Convert]::ToBoolean($True) 
    }
}

# DNS CNAME
function Add-DnsCnameIfNotExists {
     Param([Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName,
          [Parameter(Mandatory=$true)]
          [string]$DomainName,
          [Parameter(Mandatory=$true)]
          [string]$CName,
          [Parameter(Mandatory=$true)]
          [string]$CNameDestination) 

    Write-Host "Checking DNS entries... " -ForegroundColor DarkCyan

    if ($CName.ToLower() -eq "prod")
    {
        $CName = "www"
    }

    if (!(Has-AzureDnsZoneName -DnsZoneName $DomainName -ResourceGroupName $DnsResourceGroupName))
    {
        Write-Error "Create DNS zone, run the setup for it"
    }

    Set-DnsCname `
        -CName $CName `
        -HostName $CNameDestination `
        -DnsResourceGroupName $DnsResourceGroupName `
        -DomainName $DomainName
          

    Write-Host "done."
    Write-Host
} 
 
function Set-DnsCname {
    Param([Parameter(Mandatory=$true)]
          [string]$CName,
          [Parameter(Mandatory=$true)]
          [string]$HostName,
          [Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName,
          [Parameter(Mandatory=$true)]
          [string]$DomainName)        

    if (!(Has-AzureCnameDnsRecordSet `
                -CustomDomain $DomainName `
                -Name $CName `
                -ResourceGroupName $DnsResourceGroupName))
    {
        Add-DnsCname `
            -CName $CName `
            -HostName $HostName `
            -DomainName $DomainName `
            -DnsResourceGroupName $DnsResourceGroupName
    }
    elseif (Has-AzureCnameDnsRecordSetChanged  `
                -CustomDomain $DomainName `
                -Name $CName `
                -ResourceGroupName $DnsResourceGroupName `
                -HostName $HostName)
    {
        Remove-Cname -CName $CName -DnsResourceGroupName $DnsResourceGroupName

        Add-DnsCname `
            -CName $CName `
            -HostName $HostName `
            -DomainName $DomainName `
            -DnsResourceGroupName $DnsResourceGroupName
    }
}

function Add-DnsCname {
    Param([Parameter(Mandatory=$true)]
          [string]$CName,
          [Parameter(Mandatory=$true)]
          [string]$HostName,
          [Parameter(Mandatory=$true)]
          [string]$DomainName,
          [Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName) 

    Write-Host "Adding CNAME '$CName' to host '$hostName'... "
    $zone =  Get-AzureRmDnsZone -Name $DomainName -ResourceGroupName $DnsResourceGroupName
    $ttlTime =  (60 * 60 * 24) # todo: change this

    $emptyDnsRecords = @()

    $rs = New-AzureRmDnsRecordSet `
                -Name $CName `
                -RecordType CNAME `
                -ZoneName $zone.Name `
                -Ttl $ttlTime `
                -ResourceGroupName $DnsResourceGroupName `
                -DnsRecords $emptyDnsRecords

    Add-AzureRmDnsRecordConfig -RecordSet $rs -Cname $HostName  | Out-Null
    Set-AzureRmDnsRecordSet -RecordSet $rs | Out-Null
    Write-Host "done."
    Write-Host
}

function Remove-Cname {
    Param([Parameter(Mandatory=$true)]
          [string]$CName,
          [Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName)

    Write-Host "Removing CNAME '$CName'... "
    $zone =  Get-AzureRmDnsZone -Name $DomainName -ResourceGroupName $DnsResourceGroupName
    Remove-AzureRmDnsRecordSet -Name $CName -RecordType CNAME -Zone $zone 
    Write-Host "done."
    Write-Host
}

function Has-AzureCnameDnsRecordSet  {
    Param([Parameter(Mandatory=$true)]
          [string]$CustomDomain,
          [Parameter(Mandatory=$true)]
          [string]$Name,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)
    
    Write-Host "Checking if DNS Record Set for domain '$CustomDomain' with name '$Name' exists in Resource Group '$ResourceGroupName'... " -ForegroundColor White -NoNewline

    $hasService =  $false

    if ($hasService)
    {
        Write-Host $Script:ExistsInResourceTable
        Write-Host
        return [System.Convert]::ToBoolean($hasService)
    }

    try
    {
        $zone =  Get-AzureRmDnsZone -Name $CustomDomain -ResourceGroupName $ResourceGroupName
        $recordSet = Get-AzureRmDnsRecordSet -Name $Name -RecordType CNAME -Zone $zone

        if ($recordSet -ne $null) 
        {
            $hasService = $True
        }
    }
    catch 
    {
        # assume it doesn't exist, an exception is thrown for a 404
    }
   
    if ($hasService)
    {
         

        Write-Host "it exists." -NoNewline -ForegroundColor White
    }
    else
    {
        Write-Host "it doesn't exist." -NoNewline -ForegroundColor White
    }

    Write-Host
    

    $hasService
}

function Has-AzureCnameDnsRecordSetChanged  {
    Param([Parameter(Mandatory=$true)]
          [string]$CustomDomain,
          [Parameter(Mandatory=$true)]
          [string]$Name,
          [Parameter(Mandatory=$true)]
          [string]$HostName,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)

    
    
    Write-Host "Checking if DNS Record Set for domain '$CustomDomain' with name '$Name' points to '$HostName'... " -ForegroundColor White -NoNewline

    $hasNameChanged = $false
         
    try
    {
        $zone =  Get-AzureRmDnsZone -Name $CustomDomain -ResourceGroupName $ResourceGroupName
        $recordSet = Get-AzureRmDnsRecordSet -Name $Name -RecordType CNAME -Zone $zone
        
        if ($recordSet -ne $null) 
        {
            $currentCname = $recordSet.Records | select -ExpandProperty Cname

            if ($currentCname -ne $HostName)
            {
                $hasNameChanged = $true
            }
        }
    }
    catch 
    {
        # assume it doesn't exist
        Write-Error "Cannot verify that the DNS recordset is as expected, the CNAME '$Name' does not exist in the Resource Group '$ResourceGroupName'."
    }
   
    if ($hasNameChanged)
    {
        Write-Host "it does not, it points to '$currentCname'." -NoNewline -ForegroundColor White
    }
    else
    {
        Write-Host "it does." -NoNewline -ForegroundColor White
    }

    Write-Host
 
    $hasNameChanged
}

# Record set
function Has-AzureDnsRecordSet  {
    Param([Parameter(Mandatory=$true)]
          [string]$ZoneName,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)

    Write-Host "Checking if DNS Record Set for '$ZoneName' exists in Resource Group '$ResourceGroupName'... " -ForegroundColor White -NoNewline

    $hasService = $false

    if ($hasService)
    {
        Write-Host $Script:ExistsInResourceTable
        Write-Host
        return [System.Convert]::ToBoolean($hasService)
    }

    try
    {
        $zone = Get-AzureRmDnsRecordSet -ResourceGroupName $ResourceGroupName -ZoneName $ZoneName

        if ($zone -ne $null) 
        {
            $hasService = $True
        }
    }
    catch 
    {
        # assume it doesn't exist, an exception is thrown for a 404
    }
   
    if ($hasService)
    {
        
        Write-Host "it exists." -NoNewline -ForegroundColor White
    }
    else
    {
        Write-Host "it doesn't exist." -NoNewline -ForegroundColor White
    }

    Write-Host

    $hasService
}

# A Record
function Set-ARecordForCloudService {
    Param([Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName,
          [Parameter(Mandatory=$true)]
          [string]$CloudServiceName,
          [Parameter(Mandatory=$true)]
          [string]$DomainName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    if ($EnvironmentName.ToLower() -ne "prod")
    {
        return
    }

    $ipAddress = Get-VirtualIpForCloudService -CloudServiceName $CloudServiceName

    Add-DnsARecordIfNotExists `
        -DnsResourceGroupName $DnsResourceGroupName `
        -DomainName $DomainName `
        -HostName $ipAddress
}

function Add-DnsARecordIfNotExists {
     Param([Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName,
          [Parameter(Mandatory=$true)]
          [string]$DomainName,
          [Parameter(Mandatory=$true)]
          [string]$HostName) 

    Write-Host "Checking DNS entries... " -ForegroundColor DarkCyan
    
    if (!(Has-AzureDnsZoneName -DnsZoneName $DomainName -ResourceGroupName $DnsResourceGroupName))
    {
        Write-Error "Create DNS zone, run the setup for it"
    }

    Set-DnsARecord `
        -HostName $HostName `
        -DnsResourceGroupName $DnsResourceGroupName `
        -DomainName $DomainName
          
    Write-Host "done."
    Write-Host
} 
 
function Set-DnsARecord {
    Param([Parameter(Mandatory=$true)]
          [string]$HostName,
          [Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName,
          [Parameter(Mandatory=$true)]
          [string]$DomainName)        

    if (!(Has-AzureARecordDnsRecordSet `
                -CustomDomain $DomainName `
                -ResourceGroupName $DnsResourceGroupName))
    {
        Add-DnsARecord `
            -HostName $HostName `
            -DomainName $DomainName `
            -DnsResourceGroupName $DnsResourceGroupName
    }
    elseif (Has-AzureARecordDnsRecordSetChanged  `
                -CustomDomain $DomainName `
                -ResourceGroupName $DnsResourceGroupName `
                -HostName $HostName)
    {
        Remove-ARecord -DnsResourceGroupName $DnsResourceGroupName

        Add-DnsARecord `
            -HostName $HostName `
            -DomainName $DomainName `
            -DnsResourceGroupName $DnsResourceGroupName
    }
}

function Add-DnsARecord {
    Param([Parameter(Mandatory=$true)]
          [string]$HostName,
          [Parameter(Mandatory=$true)]
          [string]$DomainName,
          [Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName) 

    Write-Host "Adding A Record '$hostName'... "
    $zone =  Get-AzureRmDnsZone -Name $DomainName -ResourceGroupName $DnsResourceGroupName
    $ttlTime =  (60 * 15) # 15 min because it may change often

    $emptyDnsRecords = @()

    $rs = New-AzureRmDnsRecordSet `
                -RecordType A `
                -ZoneName $zone.Name `
                -Ttl $ttlTime `
                -ResourceGroupName $DnsResourceGroupName `
                -DnsRecords $emptyDnsRecords `
                -Name "@"
 
    Add-AzureRmDnsRecordConfig -Ipv4Address $HostName -RecordSet $rs | Out-Null
    Set-AzureRmDnsRecordSet -RecordSet $rs | Out-Null
    Write-Host "done."
    Write-Host
}

function Remove-ARecord {
    Param([Parameter(Mandatory=$true)]
          [string]$DnsResourceGroupName)

    Write-Host "Removing existing A Record... "
    $zone =  Get-AzureRmDnsZone -Name $DomainName -ResourceGroupName $DnsResourceGroupName
    Remove-AzureRmDnsRecordSet -RecordType A -Zone $zone -Name "@"
    Write-Host "done."
    Write-Host
}

function Has-AzureARecordDnsRecordSet  {
    Param([Parameter(Mandatory=$true)]
          [string]$CustomDomain,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)
    
    Write-Host "Checking if DNS Record Set for domain '$CustomDomain' with A Record exists in Resource Group '$ResourceGroupName'... " -ForegroundColor White -NoNewline

    $hasService =  $false

    if ($hasService)
    {
        Write-Host $Script:ExistsInResourceTable
        Write-Host
        return [System.Convert]::ToBoolean($hasService)
    }

    try
    {
        $zone =  Get-AzureRmDnsZone -Name $CustomDomain -ResourceGroupName $ResourceGroupName
        $recordSet = Get-AzureRmDnsRecordSet -RecordType A -Zone $zone

        if ($recordSet -ne $null) 
        {
            $hasService = $True
        }
    }
    catch 
    {
        # assume it doesn't exist, an exception is thrown for a 404
    }
   
    if ($hasService)
    {
         

        Write-Host "it exists." -NoNewline -ForegroundColor White
    }
    else
    {
        Write-Host "it doesn't exist." -NoNewline -ForegroundColor White
    }

    Write-Host
    

    $hasService
}

function Has-AzureARecordDnsRecordSetChanged  {
    Param([Parameter(Mandatory=$true)]
          [string]$CustomDomain,
          [Parameter(Mandatory=$true)]
          [string]$HostName,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)
    
    Write-Host "Checking if DNS Record Set for domain '$CustomDomain' with A Record points to '$HostName'... " -ForegroundColor White -NoNewline

    $hasChanged = $false
         
    try
    {
        $zone =  Get-AzureRmDnsZone -Name $CustomDomain -ResourceGroupName $ResourceGroupName
        $recordSet = Get-AzureRmDnsRecordSet `
                            -ZoneName ($zone.Name) `
                            -ResourceGroupName $ResourceGroupName `
                            -RecordType A

        if ($recordSet -ne $null) 
        {
            $currentARecord = ($recordSet | select -ExpandProperty Records).Ipv4Address
 
            if ($currentARecord -ne $HostName)
            {
                $hasChanged = $true
            }
        }
        else 
        {
            Write-Host "no record set"
        }
    }
    catch 
    {
        Write-Host $_.Exception.Message

        # assume it doesn't exist
        Write-Error "Cannot verify that the DNS recordset is as expected, the A Record does not exist in the Resource Group '$ResourceGroupName'."
    }
   
    if ($hasChanged)
    {
        Write-Host "it does not, it points to '$currentARecord'." -NoNewline -ForegroundColor White
    }
    else
    {
        Write-Host "it does." -NoNewline -ForegroundColor White
    }

    Write-Host
 
    $hasChanged
}

function Has-AzureDnsZoneName {
    Param([Parameter(Mandatory=$true)]
          [string]$DnsZoneName,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)

    Write-Host "Checking if DNS Zone '$DnsZoneName' exists in Resource Group '$ResourceGroupName'... " -ForegroundColor White -NoNewline

    $dnsZones = (Get-AzureRmDnsZone -ResourceGroupName $ResourceGroupName) | select -ExpandProperty Name

    if ($dnsZones -ne $null) 
    {
        $hasService = $dnsZones.Contains($DnsZoneName)
    }

    if ($hasService)
    {
        Write-Host "it exists." -NoNewline -ForegroundColor White
    }
    else
    {
        Write-Host "it doesn't exist." -NoNewline -ForegroundColor White
    }

    Write-Host

    $hasService
}
function Create-TableIfNotExists{    
    Param($TableName, 
          $StorageContext)

    Get-AzureStorageTable `
        -Name $TableName `
        -Context $StorageContext `
        -ErrorVariable ev `
        -ErrorAction SilentlyContinue | Out-Null
    
    if ($ev) {
        Write-Host "Creating table '$TableName'... " -NoNewline
        New-AzureStorageTable `
           –Name $TableName `
           –Context $StorageContext | Out-Null
        Write-Host "done." -NoNewline
        Write-Host ""
    }
    else {
        Write-Host "Table '$TableName' already exists."
    } 
}



function Create-QueueIfNotExists {
    Param([string]$QueueName,
          [string]$AzureResourceGroupName,
          [string]$AzureStorageAccountName)

    $key = (Get-AzureRmStorageAccountKey `
                    -ResourceGroupName $AzureResourceGroupName `
                    -AccountName $AzureStorageAccountName).Value[0]
    
    $ctx = New-AzureStorageContext -StorageAccountName $AzureStorageAccountName -StorageAccountKey $key
    
    $existingQueue = Get-AzureStorageQueue -Name $QueueName -Context $ctx -ErrorAction Ignore
    
    if ($existingQueue -eq $null)
    {
        Write-Host "Creating queue '$QueueName'..." -NoNewline
        $queue = New-AzureStorageQueue –Name $QueueName -Context $ctx
        Write-Host "done." -NoNewline
    }
    else 
    {
        Write-Host "queue '$QueueName' already exists." -NoNewline
    }

    Write-Host ""
}

function Has-AzureResourceGroup {
    Param([Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)
    
    Write-Host "Checking for resource group '$ResourceGroupName'..." -NoNewline
    $ResourceGroupNames = (Get-AzureRmResourceGroup | select -Expand ResourceGroupName)

    $hasAccount = $false
    
    if($ResourceGroupNames -ne $null)
    {
        $hasAccount = $ResourceGroupNames.Contains($ResourceGroupName)
    }

    return [System.Convert]::ToBoolean($hasAccount)
}
 

function Has-AzureStorageAccount {
    Param([Parameter(Mandatory=$true)]
          [string]$StorageAccountName)

    write-host "Checking for storage account '$StorageAccountName'..." -NoNewline

    $nameAvailable = [System.Convert]::ToBoolean((`
                        Get-AzureRmStorageAccountNameAvailability `
                        -Name $StorageAccountName | `
                        select -expandproperty NameAvailable))

    if ($nameAvailable)
    {
        Write-Host "it doesn't exist." -NoNewline
    }
    else 
    {
        Write-Host "it exists." -NoNewline
    }

    Write-Host ""
  
    !($nameAvailable)
}

function Create-StorageAccountIfNotExists {
    Param([string]$AzureStorageAccountName,
          [string]$AzureResourceGroupName,
          [string]$AzureDataCenter,
          [string]$StorageAccountType)

    if (!(Has-AzureStorageAccount -StorageAccountName $AzureStorageAccountName))
    {
        write-host "Creating storage account '$AzureStorageAccountName'..." -NoNewline
        
        New-AzureRmStorageAccount `
            -ResourceGroupName $AzureResourceGroupName `
            -Name $AzureStorageAccountName `
            -Location $AzureDataCenter `
            -Type $StorageAccountType | Out-Null

        Write-Host "done." -NoNewline
    }
    else 
    {
        Write-host "'$AzureStorageAccountName' exists." -NoNewline
    }

    Write-Host ""
}

function Create-ResourceGroupIfNotExists {
    Param([string]$AzureResourceGroupName,          
          [string]$AzureDataCenter)

    if (!(Has-AzureResourceGroup -ResourceGroupName $AzureResourceGroupName))
    {
        write-host "Creating resource group '$AzureResourceGroupName'..." -NoNewline
        New-AzureRmResourceGroup -Name $AzureResourceGroupName -Location $AzureDataCenter
    }
    else 
    {
        Write-Host "'$AzureResourceGroupName' exists." -NoNewline
    }

    Write-Host ""
}

function Create-BlobContainerIfNotExists {
    Param([string]$AzureResourceGroupName,
          [string]$AzureStorageAccountName,
          [string]$AzureContainerName)

    $key = Get-AzureStorageKey `
                    -AzureResourceGroupName $AzureResourceGroupName `
                    -AzureStorageAccountName $AzureStorageAccountName
    
    $ctx = New-AzureStorageContext -StorageAccountName $AzureStorageAccountName -StorageAccountKey $key

    if (-Not (Get-AzureStorageContainer -Context $ctx | Where-Object { $_.Name -eq $AzureContainerName }) ) {
        
            write-host "Creating new storage container '$AzureContainerName'..." -NoNewline
            New-AzureStorageContainer -Name $AzureContainerName -Context $ctx | Out-Null
            Write-Host "done." -NoNewline
    }
    else 
    {
            Write-Host "container '$AzureContainerName' exists." -NoNewline
    }

    Write-Host ""
}

function Get-AzureStorageKey {
    Param([string]$AzureResourceGroupName,
          [string]$AzureStorageAccountName)

    (Get-AzureRmStorageAccountKey `
                    -ResourceGroupName $AzureResourceGroupName `
                    -AccountName $AzureStorageAccountName).Value[0]
}

function Upload-AzureWebsitePackage {
    Param([string]$AzureResourceGroupName,
          [string]$AzureStorageAccountName,
          [string]$WebsitePackagePath,
          [string]$StorageContainerName)

    $key = Get-AzureStorageKey `
                    -AzureResourceGroupName $AzureResourceGroupName `
                    -AzureStorageAccountName $AzureStorageAccountName          
        
    $ctx = New-AzureStorageContext `
                        -StorageAccountName $AzureStorageAccountName `
                        -StorageAccountKey $key

    $fileName = Split-Path $WebsitePackagePath -leaf

    write-host "Uploading package '$WebsitePackagePath'..." -NoNewline 

    Set-AzureStorageBlobContent -File $WebsitePackagePath `
                                -Container $StorageContainerName `
                                -Blob $fileName `
                                -Context $ctx `
                                -Force | Out-Null

    Write-Host "done." -NoNewline     
    write-host ""  

    $url = (New-AzureStorageBlobSASToken `
                            -Container $StorageContainerName `
                            -Blob $fileName `
                            -Context $ctx `
                            -Permission r `
                            -ExpiryTime (Get-Date).AddHours(2.0) `
                            -FullUri)
    
    $url
}


function Wait-ForAzureQueueMessage {    
    Param([string]$QueueName,
          [string]$AzureResourceGroupName,
          [string]$AzureStorageAccountName,
          [string]$MessageId,
          [int]$MaxMinutesToWait)

    $Message = Get-JsonMessageFromId -Id $MessageId

    $key = Get-AzureStorageKey `
                    -AzureResourceGroupName $AzureResourceGroupName `
                    -AzureStorageAccountName $AzureStorageAccountName
    
    $ctx = New-AzureStorageContext `
                -StorageAccountName $AzureStorageAccountName `
                -StorageAccountKey $key
    
    $queue = Get-AzureStorageQueue -Name $QueueName -Context $ctx                  

    $StopWatch = New-Object -TypeName System.Diagnostics.Stopwatch 
    $StopWatch.Start()
    
    Write-Host "Waiting for message: '$Message' in queue: '$QueueName'..." -NoNewline

    $invisibleTimeout = [System.TimeSpan]::FromSeconds(1)

    while ($True)
    {
        $queueMessage = $queue.CloudQueue.GetMessage($invisibleTimeout)        
        
        $retrievedMessage = $queueMessage.AsString
        
        if ($retrievedMessage -ne $null -and `
            $retrievedMessage.Contains($MessageId))
        {
            $queue.CloudQueue.DeleteMessage($queueMessage)
            break
        }

        Write-Host (Get-UtcDate)"...$retrievedMessage" -NoNewline
        Start-Sleep -Seconds 10

        if ($StopWatch.Elapsed.TotalMinutes -gt $MaxMinutesToWait)
        {
            Write-Error "timeout!"
        }
    }

    Write-Host "done." -NoNewline
    Write-Host ""
}



function Remove-AzureResource {
    Param([Parameter(Mandatory=$true)]
          [string]$ResourceId)

    Remove-AzureRmResource -ResourceId $ResourceId -force | Out-Null
    Write-Host "deleted: " $ResourceId
}


function Add-AzureQueueMessage {
    Param([string]$QueueName,
          [string]$AzureResourceGroupName,
          [string]$AzureStorageAccountName,
          [string]$MessageId)

    $key = Get-AzureStorageKey `
                    -AzureResourceGroupName $AzureResourceGroupName `
                    -AzureStorageAccountName $AzureStorageAccountName
    
    $ctx = New-AzureStorageContext -StorageAccountName $AzureStorageAccountName -StorageAccountKey $key
    
    $queue = Get-AzureStorageQueue -Name $QueueName -Context $ctx

    $Message = Get-JsonMessageFromId -Id $MessageId
    
    if ($Script:AzurePowerShellVersion -eq "5.1.1")
    {   
        Write-Host "other"
        $queueMesssage = $Message
    }
    else 
    {
        $queueMesssage = New-Object -TypeName Microsoft.WindowsAzure.Storage.Queue.CloudQueueMessage `
                                -ArgumentList $Message
    }

    Write-Host "Adding message to queue: '$Message'..." -NoNewline
    $queue.CloudQueue.AddMessage($queueMesssage)
    Write-Host "done." -NoNewline
    Write-Host ""
}

function Get-VirtualIpForWebsite  {
    Param([Parameter(Mandatory=$true)]
          [string]$WebsiteName,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)
        
    $outboundIpAddresses = ((Get-AzureRmResource `
                                -ResourceGroupName $ResourceGroupName `
                                -ResourceType Microsoft.Web/sites `
                                -ResourceName $WebsiteName).Properties.outboundIpAddresses)

    $outboundIpAddress = $outboundIpAddresses.Split(',')[0]
    Write-Host "IP address $outboundIpAddress..."

    return $outboundIpAddress
}

function Get-StorageAccountConnectionString {
    Param([Parameter(Mandatory=$true)]
          [string]$AzureStorageAccountName,
          [Parameter(Mandatory=$true)]
          [string]$AzureResourceGroupName)

    $key = Get-AzureStorageKey `
                    -AzureResourceGroupName $AzureResourceGroupName `
                    -AzureStorageAccountName $AzureStorageAccountName

    "DefaultEndpointsProtocol=https;AccountName=$AzureStorageAccountName;AccountKey=$key;EndpointSuffix=core.windows.net";
}


 
function Set-WebsiteSslBinding {
    Param([Parameter(Mandatory=$true)]
          [string]$SiteName,
          [Parameter(Mandatory=$true)]
          [string]$ResourceGroupName)
 
    # note: this is not needed right now
    return

    Write-host "Setting binding for $SiteName..."
 
    $envJson = Get-Content $DefaultTemplateParameterFile | ConvertFrom-Json
    $sSecStrPassword = $envJson.parameters.pfxPassword.value
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
    $cert.Import($pfxPath, $sSecStrPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)
    
    $ar = Get-AzureRmResource `
                -Name $SiteName `
                -ResourceGroupName $ResourceGroupName `
                -ResourceType Microsoft.Web/sites `
                -ApiVersion 2014-11-01
    
    $props = $ar.Properties
    
    $props.HostNameSslStates[2].'SslState' = 1
    $props.HostNameSslStates[2].'thumbprint' = $cert.Thumbprint
    $props.hostNameSslStates[2].'toUpdate' = $true
    
    Set-AzureRmResource `
            -ApiVersion 2014-11-01 `
            -Name $SiteName `
            -ResourceGroupName $ResourceGroupName `
            -ResourceType Microsoft.Web/sites `
            -PropertyObject $props -Force | Out-Null
}



#####################
# naming conventions
#####################



function Get-ServiceBusName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    "$AppShortName".ToLower() + "$EnvironmentName".ToLower() + "sb"
}

function Get-DatabaseName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    "$AppShortName".ToLower() + "$EnvironmentName".ToLower() + "_db"
}

function Get-DatabaseServerName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    "$AppShortName".ToLower() + "$EnvironmentName".ToLower() + "dbsvr"
}

function Get-HostingPlanName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-" + "hosting-plan"
}

function Get-PortalSiteName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName,
          [Parameter(Mandatory=$true)]
          [string]$DataCenter)

    "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-portal-" + $DataCenter.ToLower().Replace(" ", "-")
}

function Get-TrafficSiteName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName,
          [Parameter(Mandatory=$true)]
          [string]$DataCenter)

    "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-traffic-" + $DataCenter.ToLower().Replace(" ", "-")
}

function Get-SendGridName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-sendgrid"
}

function Get-PortalDnsRecordName {
    Param([Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    if ($EnvironmentName.ToLower() -eq $prodEnvironmentName)
    {
        return "www"
    }
    else 
    {
        return $EnvironmentName.ToLower()
    }
}

function Get-TrafficDnsRecordName {
    Param([Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    if ($EnvironmentName.ToLower() -eq $prodEnvironmentName)
    {
        return "go"
    }
    else 
    {
        return $EnvironmentName.ToLower() + "-go"
    }
}

function Get-ApiDnsRecordName {
    Param([Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    if ($EnvironmentName.ToLower() -eq $prodEnvironmentName)
    {
        return "api"
    }
    else 
    {
        return $EnvironmentName.ToLower() + "-api"
    }
}

function Get-GetAppResourceGroupName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName,
          [Parameter(Mandatory=$true)]
          [string]$DataCenter)

    "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-" + $DataCenter.ToLower().Replace(" ", "-") + "-app-group"
}

function Get-GetDnsResourceGroupName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$DataCenter)

    "$AppShortName".ToLower() + "-" + $DataCenter.ToLower().Replace(" ", "-") + "-dns-group"
}

function Get-GetAppStorageAccountName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName,
          [Parameter(Mandatory=$true)]
          [string]$DataCenter)

    "$AppShortName".ToLower() + "$EnvironmentName".ToLower() + $DataCenter.ToLower().Replace(" ", "") + "app"
} 

function Get-CdnHostName {
    Param([Parameter(Mandatory=$true)]
          [string]$EnvironmentName,
          [Parameter(Mandatory=$true)]
          [string]$CustomDomain)

    if ($EnvironmentName.ToLower() -eq $prodEnvironmentName)
    {
        return "cdn.$CustomDomain".ToLower()
    }
    else
    {
        return "$EnvironmentName".ToLower() + "-cdn.$CustomDomain".ToLower()
    }
} 

function Get-CdnDnsRecordName {
    Param([Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    if ($EnvironmentName.ToLower() -eq $prodEnvironmentName)
    {
        return "cdn"
    }
    else
    {
        return "$EnvironmentName".ToLower() + "-cdn"
    }
} 

function Get-CdnEndpointName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    return "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-cdn-endpoint"
} 

function Get-CdnProfileName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    return "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-cdn-profile"
} 
function Get-JobName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-job"
} 


function Get-JobCollectionName {
    Param([Parameter(Mandatory=$true)]
          [string]$AppShortName,
          [Parameter(Mandatory=$true)]
          [string]$EnvironmentName)

    "$AppShortName".ToLower() + "-" + "$EnvironmentName".ToLower() + "-job-collection"
} 







################################################################################
# Register-AzureServicePrincipal
# Given the correct input, does one of the following: 
# 1> checks for existence of application registration
# 2> checks to see if the app is registered as a service principal
# 3> if neither of those is true, creates the app and service principal
# > outputs an object with all details possible. If the app already exists the 
# password will be null because we can't look it up. 
#  INPUT: servicePrincipalName, the displayname of the desired App/ServicePrincipal and the desired password.
#       The password field is optional and if omitted a 30 character random password will be generated and returned. 
#  OUTPUT: an object containing the following NoteProperties
#       > ClientID: the GUID representing the application ID
#       > ServicePrincipalID: the GUID representing the Service Principal association
#       > SPNNames: The service principal names of the SP
#       > ServicePrincipalPassword: A securestring of the Application Password. NOTE: This will be NULL if the app is already registered in AD as we cannot retrieve it.
#       > ServicePrincipalAlreadyExists: boolean to indicate if the sp already existed or not
# USAGE NOTES: Assumes already logged into Azure with proper permissions and that the desired subscription is selected. 

Function Register-AzureServicePrincipal{
    param(
        # The name for the service principal. We won't make this mandatory to allow for manual entry mode with guidance. Obviously it needs to be specified for automation. 
        [string]$servicePrincipalName,
        # the password if you choose to specify it, otherwise the script will generate one for you. 
        [string]$servicePrincipalPassword,
        [string]$identifierUri
    )
    # Set the regex for the input validation on the SPN
    $SPNNamingStandard='^[--z]{5,40}$'
    Write-Host "Provisioning AzureAD App/Service Principal"
    Write-Warning "The account operating this script MUST have the role Subscription Admin or Owner in the desired subscription"
    $ErrorActionPreference = "Stop" # Error handing is not yet sufficient; try/catch the stuff below!
    if (!$servicePrincipalName){
        do {
            Write-Host "SPN naming standard is (in RegEx): $SPNNamingStandard"
            $servicePrincipalName=Read-Host "Service Principal Name not specified on startup; Please enter desired name or type GUID and press enter for a guid based random name"
            if ($servicePrincipalName -eq "GUID"){
                $guid=([guid]::NewGuid()).toString()
                $servicePrincipalName="SPN-$guid"
            }
        } until ($servicePrincipalName -match $SPNNamingStandard)
    }
    # handle command line specification of GUID 
    if ($servicePrincipalName -eq "GUID"){
        $guid=([guid]::NewGuid()).toString()
        $servicePrincipalName="SPN-$guid"
    }
    # set URL and IdentifierUris
    $homePage = "http://" + $servicePrincipalName    

    Write-Host "Desired Service Principal Name is $servicePrincipalName `n"
    
    # Now we need to determine if 1> the Application exists and 2> if it has been registered as a service principal. This will guide our execution through the end of the function. 
    $appExists=Get-AzureRmADApplication -DisplayNameStartWith $servicePrincipalName -ErrorAction SilentlyContinue
    
    # check for SPN only if app exists. SPN can't exist without app so no reason to check if not. 
    if ($appExists){$spnExists=Get-AzureRmADServicePrincipal | Where-Object {$_.ApplicationId -eq $appExists.ApplicationId} -ErrorAction SilentlyContinue}

    # we only need a password if the app hasn't been created yet.
    if (!$appExists){
        # Generate a password if needed
        if (!$servicePrincipalPassword){
            $servicePrincipalPassword=New-RandomPassword -passwordLength 40
        }
        # NOTE! We had a convertto-securestring here but as it turns out new-azurermadapplication doesn't take a securestring, only a string
        # NOTEUPDATE! AzureRM 5.0 and higher requires a securestring (yay!) This has been updated but notes left here for reference.
        $securePassword=ConvertTo-SecureString -String $servicePrincipalPassword -AsPlainText -Force                                  
    }
    # we set this to NULL as a "valid" return as the appID already exists and we can't lookup the password from here 
    else {$servicePrincipalPassword=$null}

    
    # Create the App if it wasn't already
    if (!$appExists){
    Write-Host "a"
        $securePassword=ConvertTo-SecureString -String $servicePrincipalPassword -AsPlainText -Force        
        Write-Host "b"
        $azureADApplication=New-AzureRmADApplication -DisplayName "deployuser" -HomePage $homePage -IdentifierUris $identifierUri -Password $securePassword
        Write-Host "c"
        Write-Host "Azure AAD Application creation completed successfully"
    }
    # if it already exists we'll just redirect the variable
    else{$azureADApplication=$appExists}
    $appID=$azureADApplication.ApplicationId

    # Create new SPN if needed
    if (!$spnExists){
        $spn=New-AzureRmADServicePrincipal -ApplicationId $appId
        Write-Host "SPN creation completed successfully"
    }
    else{$spn=$spnExists}
    $spnNames=$spn.ServicePrincipalNames

    # Create object to store information. 
    $outputObject=New-Object -TypeName PSObject
    $outputObject | Add-Member -MemberType NoteProperty -Name ServicePrincipalName -Value $servicePrincipalName
    $outputObject | Add-Member -MemberType NoteProperty -Name ClientID -Value $appID
    $outputObject | Add-Member -MemberType NoteProperty -Name ServicePrincipalID -Value $spn.Id
    $outputObject | Add-Member -MemberType NoteProperty -Name SPNNames -Value $spnNames
    $outputObject | Add-Member -MemberType NoteProperty -Name ServicePrincipalPassword -Value $servicePrincipalPassword
    if ($appExists -and $spnexists){$outputObject | Add-Member -MemberType NoteProperty -Name ServicePrincipalAlreadyExists -Value $true}
    else {$outputObject | Add-Member -MemberType NoteProperty -Name ServicePrincipalAlreadyExists -Value $false}

    return $outputObject
}
################################################################################




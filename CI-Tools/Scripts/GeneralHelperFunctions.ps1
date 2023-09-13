##########################
# General Helper Functions
##########################

function Get-RandomLetters {
    Param([int]$Count = 2)

    (-join ((65..90) + (97..122) | Get-Random -Count $Count | % {[char]$_})).ToString().ToLower()

}

function Stop-ProcessSafely {
    Param($Name)

    $runningProcess = Get-Process -Name $Name -ErrorAction Ignore

    if ($runningProcess)
    {
        Stop-Process -Name $Name
    }
}

function Assign-VersionValue([string]$oldValue, [string]$newValue) {
    if ($newValue -eq $null -or $newValue -eq "") {
        $oldValue
    } else {
        #placeholder for other functionality, like incrementing, dates, etc..
        if ($newValue -eq "increment") {
            $newNum = 1
            try {
                $newNum = [System.Convert]::ToInt64($oldValue) + 1
            } catch {
                #do nothing
            }
            $newNum.ToString()
        } else {
            $newValue
        }
    }
}

function Get-UtcDate {

    $now = Get-date
    $utcTime = $now.ToUniversalTime().ToString("s")

    $utcTime
}

function Set-FileSettings($fileLocation)
{
    Write-Host 'db string ' +  $dbConnectionString
    $envJson = Get-Content $fileLocation | ConvertFrom-Json
    $envJson.ConnectionStrings.SqlServerConnection = $dbConnectionString
    $envJson.NeutrinoApi.UserId = $NeutrinoApiUserId
    $envJson.NeutrinoApi.ApiKey = $NeutrinoApiApiKey
    $envJson | ConvertTo-Json | set-content $fileLocation
    Write-Host "Saving $fileLocation..."
}
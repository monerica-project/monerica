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
    $envJson.ConnectionStrings.DefaultConnection = $dbConnectionString
    $envJson | ConvertTo-Json | set-content $fileLocation
    Write-Host "Saving $fileLocation..."
}

function Retry-Command {
    [CmdletBinding()]
    Param(
        [Parameter(Position=0, Mandatory=$true)]
        [scriptblock]$ScriptBlock,

        [Parameter(Position=1, Mandatory=$false)]
        [int]$Maximum = 5,

        [Parameter(Position=2, Mandatory=$false)]
        [int]$Delay = 100
    )

    Begin {
        $cnt = 0
    }

    Process {
        do {
            $cnt++
            try {
                # If you want messages from the ScriptBlock
                # Invoke-Command -Command $ScriptBlock
                # Otherwise use this command which won't display underlying script messages
                $ScriptBlock.Invoke()
                return
            } catch {
                Write-Host "Failed, retrying $cnt of $Maximum..."
                Start-Sleep -Milliseconds $Delay
            }
        } while ($cnt -lt $Maximum)

        # Throw an error after $Maximum unsuccessful invocations. Doesn't need
        # a condition, since the function returns upon successful invocation.
        throw 'Execution failed.'
    }
}
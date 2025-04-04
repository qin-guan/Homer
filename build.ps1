<#
.SYNOPSIS
Build helper

.DESCRIPTION
USAGE
    .\build.ps1 <command>

COMMANDS
    dev             run `dotnet run --project .\Homer.NetDaemon`
    watch           run `dotnet watch --project .\Homer.NetDarmon`
    help, -?        show this help message
#>

[CmdletBinding()]
Param(
    [Parameter(Position = 0)]
    [ValidateSet("dev", "watch", "help")]
    [string]$Command,
    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

function Command-Dev
{
    dotnet run --project ./Homer.NetDaemon $Arguments
}

function Command-Watch
{
    dotnet watch --project ./Homer.NetDaemon $Arguments
}

function Command-Help
{
    Get-Help $PSCommandPath
}

Switch ($Command)
{
    "dev"  {
        Command-Dev
    }
    "watch" {
        Command-Watch
    }
    "help" {
        Command-Help
    }
    default {
        Command-Help
    }
}
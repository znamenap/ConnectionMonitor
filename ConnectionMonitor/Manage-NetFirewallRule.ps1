[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
param(
	[Parameter(Mandatory=$true, ParameterSetName='Add')]
	[switch] $Add,

	[Parameter(Mandatory=$true, ParameterSetName='Remove')]
	[switch] $Remove,

	[Parameter(Mandatory=$true, ParameterSetName='Enable')]
	[switch] $Enable,

	[Parameter(Mandatory=$true, ParameterSetName='Disable')]
	[switch] $Disable,
	
	[Parameter(Mandatory=$false, ParameterSetName='Add')]
	[Parameter(Mandatory=$false, ParameterSetName='Remove')]
	[Parameter(Mandatory=$false, ParameterSetName='Enable')]
	[Parameter(Mandatory=$false, ParameterSetName='Disable')]
	[ValidateNotNullOrEmpty()]
	[string] $Name = "ConnectionMonitorInbound",

	[Parameter(Mandatory=$false, ParameterSetName='Add')]
	[Parameter(Mandatory=$false, ParameterSetName='Remove')]
	[int] $LocalPort = 3859,

	[Parameter(Mandatory=$false, ParameterSetName='Add')]
	[Parameter(Mandatory=$false, ParameterSetName='Remove')]
	[string] $DisplayName = "ConnectionMonitor Inbound",

	[Parameter(Mandatory=$false, ParameterSetName='Add')]
	[Parameter(Mandatory=$false, ParameterSetName='Remove')]
	[string] $Description = "$DisplayName Allow Inbound $LocalPort",

	[Parameter(Mandatory=$false, ParameterSetName='Add')]
	[switch] $Force
)
process {
	$RuleDescription = "`$Name=$Name, `$LocalPort=$LocalPort, `$DisplayName=$DisplayName, `$Description=$Description"
	Write-Debug "Firewall Rule: $RuleDescription"

	if ($Add.IsPresent) {
		$Rule=Get-NetFirewallRule -Name $Name
		if ($Rule -and $Force.IsPresent) {
			if ($PSCmdLet.ShouldProcess($RuleDescription,"Remove Firewall Rule")) {
				Remove-NetFirewallRule -Name $Name
			}
		}
		$NewParams = @{
			DisplayName = $DisplayName
			Name = $Name
			Description = $Description
			Action = 'Allow'
			Direction = 'Inbound'
			Profile = 'Any'
			Protocol = 'TCP'
			EdgeTraversalPolicy = 'Allow'
			LocalPort = $LocalPort
		}
		if ($PSCmdLet.ShouldProcess($RuleDescription,"Add Firewall Rule")) {
			New-NetFirewallRule @NewParams
		}
	} elseif ($Enable.IsPresent) {
		if ($PSCmdLet.ShouldProcess($RuleDescription,"Allow Firewall Rule")) {
			Set-NetFirewallRule -Name $name -Action Allow
		}
	} elseif ($Disable.IsPresent) {
		if ($PSCmdLet.ShouldProcess($RuleDescription,"Block Firewall Rule")) {
			Set-NetFirewallRule -Name $name -Action Block
		}
	} elseif ($Remove.IsPresent) {
		if ($PSCmdLet.ShouldProcess($RuleDescription,"Remove Firewall Rule")) {
			Remove-NetFirewallRule -Name $Name
		}
	}
}
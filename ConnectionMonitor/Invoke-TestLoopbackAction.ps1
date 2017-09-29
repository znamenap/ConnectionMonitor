[CmdletBinding()]
param(
    [Parameter(ParameterSetName='Enable')]
    [switch] $Enable,

    [Parameter(ParameterSetName='Disable')]
    [switch] $Disable,

    [Parameter(ParameterSetName='Install')]
    [switch] $Install,

    [Parameter(ParameterSetName='Remove')]
    [switch] $Remove,

    [Parameter(ParameterSetName='Enable')]
    [Parameter(ParameterSetName='Disable')]
    [Parameter(ParameterSetName='Install')]
    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [string] $AdapterName = "TestLoopback",

    [Parameter(ParameterSetName='Install')]
    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [string] $IPAddress = "192.168.30.1", 

    [Parameter(ParameterSetName='Install')]
    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [string] $SubnetMask = "255.255.255.0",

    [Parameter(ParameterSetName='Install')]
    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [string] $GatewayIPAddress = "192.168.30.254"
)

if ($PSCmdlet.ParameterSetName -eq "Install" ) {
    choco.exe install devcon.portable

    # http://support.microsoft.com/kb/311272/en-us
    # http://munashiku.slightofmind.net/20090621/sometimes-64-bit-is-a-pain
    devcon64.exe -r install $env:WinDir\Inf\netloop.inf *MSLOOP
    # devcon64.exe -r remove $env:WinDir\Inf\netloop.inf *MSLOOP

    $Adapter = Get-NetAdapter -InterfaceDescription "Microsoft KM-TEST Loopback Adapter" |
        Rename-NetAdapter -PassThru -NewName $AdapterName

    $nic = Get-WmiObject Win32_NetworkAdapterConfiguration -Filter "InterfaceIndex='$($adapter.InterfaceIndex)'"
    ## Set the metric to 254
    $nic.SetIPConnectionMetric(254)
    ## Set the "Register this connection's address in DNS" to unchecked
    $nic.SetDynamicDNSRegistration($false)
    # disable bindings
    Disable-NetAdapterBinding -Name $Adapter.Name -ComponentID ms_rdma_ndk, ms_lldp, ms_pppoe, ms_msclient, ms_pacer, ms_server, ms_tcpip6, ms_lltdio, ms_rspndr
    # Assign an IP address, subnet mask, and gateway
    $nic.EnableStatic($IPAddress,$SubnetMask)
    $nic.SetGateways($GatewayIPAddress)
    
    Get-NetAdapter -InterfaceIndex $Adapter.InterfaceIndex
}
elseif ($PSCmdlet.ParameterSetName -eq 'Remove') 
{
    Disable-NetAdapter -Name $AdapterName
    devcon64.exe -r remove $env:WinDir\Inf\netloop.inf *MSLOOP
}
elseif ($PSCmdlet.ParameterSetName -eq 'Disable')
{
    Disable-NetAdapter -Name $AdapterName -PassThru -Confirm:$false
}
elseif ($PSCmdlet.ParameterSetName -eq 'Enable')
{
    Enable-NetAdapter -Name $AdapterName -PassThru -Confirm:$false |
        ForEach-Object { Start-Sleep -Seconds 3; Get-NetAdapter -Name $AdapterName }            
}

#TODO : Modify WMI management with following guideline
# https://www.pdq.com/blog/using-powershell-to-set-static-and-dhcp-ip-addresses-part-1/

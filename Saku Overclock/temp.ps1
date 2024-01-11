$data = Get-WMIObject -Query "SELECT * FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation" -Namespace "root/CIMV2"
@($data)[0].HighPrecisionTemperature/10-273
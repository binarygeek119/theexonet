param(
  [Parameter(Mandatory = $true)]
  [string]$Token,
  [string]$ApiBase = "https://ravaapi.binarygeek119.duckdns.org"
)

$headers = @{
  Authorization = "Bearer $Token"
  Accept        = "application/json"
}

function Get-Json($Path) {
  try {
    return Invoke-RestMethod -Uri "$ApiBase$Path" -Headers $headers -Method Get
  } catch {
    return @{ error = $_.Exception.Message; status = $_.Exception.Response.StatusCode.value__ }
  }
}

$access = Get-Json "/api/admin/access"
$friends = Get-Json "/api/player/friends"
$profile = Get-Json "/api/player/profile"

$friendList = @($friends.friends)
$testingDummies = @($friendList | Where-Object { $_.isTestingDummy -eq $true })

[pscustomobject]@{
  isAdmin              = $access.isAdmin
  testingModeEnabled   = $access.testingModeEnabled
  profileIsStaffAdmin  = $profile.isStaffAdmin
  profileTestingMode   = $profile.testingModeEnabled
  friendCount          = $friendList.Count
  testingDummyCount    = $testingDummies.Count
  sampleFriends        = ($friendList | Select-Object -First 5 username, isTestingDummy | ConvertTo-Json -Compress)
}

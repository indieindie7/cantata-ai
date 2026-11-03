# Remove BepInEx + mod from Cantata (clean uninstall). Game files were never modified.
$g = 'C:\Program Files (x86)\Steam\steamapps\common\Cantata'
Get-Process cantata -ErrorAction SilentlyContinue | Stop-Process
Remove-Item -Recurse -Force "$g\BepInEx" -ErrorAction SilentlyContinue
foreach ($f in 'winhttp.dll','doorstop_config.ini','changelog.txt') { Remove-Item -Force "$g\$f" -ErrorAction SilentlyContinue }

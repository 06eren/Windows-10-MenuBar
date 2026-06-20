Write-Host 'Windows 10 Menu Bar Kurulumuna Hosgeldiniz!' -ForegroundColor Cyan
$installPath = 'C:\Program Files\Windows10MenuBar'
if (-not (Test-Path $installPath)) {
    New-Item -ItemType Directory -Force -Path $installPath | Out-Null
}
Copy-Item -Path '.\*' -Destination $installPath -Recurse -Force
Write-Host 'Dosyalar kopyalandi...' -ForegroundColor Green

$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("C:\Users\karga\Desktop\Windows 10 Menu Bar.lnk")
$Shortcut.TargetPath = "$installPath\Windows-10-MenuBar.exe"
$Shortcut.WorkingDirectory = "$installPath"
$Shortcut.Save()
Write-Host 'Masaustu kisayolu olusturuldu!' -ForegroundColor Green

Write-Host 'Kurulum tamamlandi! Masaustundeki kisayoldan calistirabilirsiniz.' -ForegroundColor Yellow
Read-Host 'Cikmak icin Enter a basin...'

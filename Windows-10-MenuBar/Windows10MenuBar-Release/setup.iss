[Setup]
AppName=Windows 10 Menu Bar
AppVersion=1.0
AppPublisher=ERN YAZILIM
DefaultDirName={autopf}\Windows10MenuBar
DefaultGroupName=Windows 10 Menu Bar
OutputDir=C:\Users\karga\Desktop
OutputBaseFilename=Windows10MenuBar_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
LicenseFile=C:\Users\karga\Desktop\Windows10MenuBar-Release\license.txt
PrivilegesRequired=admin
DisableWelcomePage=no

[Tasks]
Name: "desktopicon"; Description: "Masaustune kisayol olustur"; GroupDescription: "Ek Gorevler:"

[Files]
Source: "C:\Users\karga\Desktop\Windows10MenuBar-Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Windows 10 Menu Bar"; Filename: "{app}\Windows-10-MenuBar.exe"
Name: "{autodesktop}\Windows 10 Menu Bar"; Filename: "{app}\Windows-10-MenuBar.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Windows-10-MenuBar.exe"; Description: "Windows 10 Menu Bar uygulamasini baslat"; Flags: nowait postinstall skipifsilent

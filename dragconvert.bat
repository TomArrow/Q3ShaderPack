rem cd %~dp0
FOR %%A IN (%*) DO ^
Q3ShaderPack "%%~A" "C:\Q3\baseq3\scripts" "C:\JK2\GameData\base\shaders" "out:out" -ignoreShaderList -q32jk2
pause
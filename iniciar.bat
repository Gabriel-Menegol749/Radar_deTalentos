@echo off
echo =======================================================
echo Iniciando o Radar de Talentos...
echo.
echo MANTENHA ESTA JANELA ABERTA PARA O SISTEMA FUNCIONAR.
echo Para desligar o sistema, basta fechar esta janela no X.
echo =======================================================
echo.
echo Aguarde... O sistema esta compilando.
echo O Google Chrome abrira sozinho assim que tudo estiver pronto!

:: Fica testando a porta 5080 de forma invisivel. Quando a API responder, abre o Chrome.
start /b powershell -WindowStyle Hidden -Command "while ($true) { try { $t = New-Object System.Net.Sockets.TcpClient('127.0.0.1', 5080); $t.Close(); break; } catch { Start-Sleep -Seconds 1 } }; Start-Process 'chrome' 'http://localhost:5173'"

:: Entra na pasta do frontend e roda os servidores
cd frontend
npx --yes concurrently -k -c "blue.bold,green.bold" -n "API,SITE" "cd ../backend/RadarTalentos.API && dotnet run --launch-profile http" "npm run dev"
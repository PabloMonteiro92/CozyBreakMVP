# Validação local — CozyBreak

A refatoração mandatória foi aplicada ao projeto.

## Verificações concluídas

- Os arquivos `MainWindow.xaml`, `App.xaml` e `WaterPetOverlay.xaml` foram analisados como XML válido.
- O novo `WaterPetOverlay` está presente com janela transparente, `NearestNeighbor`, animação de entrada e ações de confirmação/adiamento.
- `ShowBalloonTip` foi removido.
- Os handlers dos timers são registrados uma única vez no construtor; atualizações posteriores apenas interrompem, reconfiguram e reiniciam os timers.
- O `ContextMenuStrip` é criado uma única vez e descartado em `Cleanup`.
- `Cleanup` interrompe timers, fecha o overlay e libera os recursos da bandeja.
- Foram adicionados testes para IMC, intervalo efetivo, rotação de exercícios e parsing com ponto/vírgula.

## Limitação do ambiente

A imagem de execução atual não contém `dotnet`, `msbuild` ou `csc`. Portanto, `dotnet test --configuration Release` não pôde ser executado neste ambiente. A validação final deve ser executada em Windows ou runner com .NET 8 SDK:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet publish src/CozyBreak/CozyBreak.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64
```

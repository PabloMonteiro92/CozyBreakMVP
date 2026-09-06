# CozyBreak MVP

Assistente desktop local-first para lembretes de hidratação e pausas confortáveis. O MVP é uma aplicação WPF em C#/.NET 8 para Windows, com persistência local e distribuição self-contained single-file.

## Escopo implementado

O fluxo permite cadastrar um apelido, idade, peso, altura e áreas opcionais de desconforto. As preferências de intervalo podem ser ajustadas. Depois de salvar, o aplicativo permanece no system tray e exibe lembretes de água e pausas. O menu permite reabrir o painel, pausar alertas por uma hora, forçar uma pausa e sair.

A meta de água é uma **estimativa configurável de bem-estar** baseada em peso. Ela não é uma prescrição e não deve ser usada para pessoas com condições médicas sem orientação profissional. O usuário deve interromper qualquer exercício diante de dor, tontura, perda de força ou formigamento.

## Abrir e compilar

Requer Windows 10/11 compatível e .NET 8 SDK.

```powershell
git clone <url-do-repositorio>
cd CozyBreakMVP
dotnet restore
dotnet build --configuration Release
dotnet publish src/CozyBreak/CozyBreak.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64
```

O artefato principal será `artifacts/win-x64/CozyBreak.exe`. Calcule o hash antes de distribuir:

```powershell
Get-FileHash artifacts/win-x64/CozyBreak.exe -Algorithm SHA256
```

Para assinatura Authenticode, importe o certificado corporativo no perfil do runner Windows e execute `scripts/sign-release.ps1`. O certificado nunca deve ser commitado no repositório. A assinatura só é considerada válida quando o Windows confirmar o status `Valid` e o timestamp estiver presente.

## Dados e privacidade

O perfil é gravado em `%LocalAppData%/CozyBreak/perfil.json.dpapi`, protegido com DPAPI no escopo do usuário atual. Não há servidor, conta, telemetria ou chamada de rede. O arquivo não grava IMC. A migração do antigo `perfil.json` ocorre uma vez e o arquivo legado é removido.

## Segurança de release

O workflow do GitHub Actions restaura, compila, publica, calcula SHA-256 e executa auditoria de pacotes. A assinatura Authenticode não foi automatizada porque exige certificado e segredo corporativos. A integração com VirusTotal deve ser opcional; upload externo não é necessário para o funcionamento e não deve ser descrito como garantia de ausência de ameaças.

Leia [ANALISE-E-PLANO.md](ANALISE-E-PLANO.md) antes de criar a primeira release.

## Validação clínica, acessibilidade e Windows

O texto dos exercícios foi limitado a movimentos suaves e inclui orientação para interromper diante de dor, tontura ou formigamento. Antes de uso corporativo, um profissional habilitado deve revisar o conteúdo e o posicionamento do produto como ferramenta de bem-estar, não como dispositivo médico. Os controles principais possuem nomes para leitores de tela e são navegáveis por teclado. A janela de pausa é criada na tela ativa; ainda é necessário validar múltiplos monitores, escala DPI, suspensão/retomada e leitores de tela em Windows real.

# CozyBreak — análise técnica e plano do MVP

## Conclusão executiva

A especificação é viável como aplicativo desktop local-first, mas **não deve ser implementada literalmente** antes de corrigir quatro pontos: o produto não pode se apresentar como dispositivo médico; os dados de saúde e medidas corporais precisam de minimização e proteção local; a lógica de hidratação precisa ser tratada como lembrete configurável, não como prescrição; e a distribuição precisa separar compilação, assinatura e verificação de integridade.

O MVP entregue nesta pasta implementa um núcleo funcional em **C#/.NET 8 WPF**, com onboarding, perfil local protegido por DPAPI, cálculo de meta de hidratação como estimativa configurável, lembretes de água e pausa, modo apresentação, recomendação cíclica de exercícios, modulação do intervalo por IMC, persistência atômica, validação invariant de entrada, acessibilidade básica, suporte inicial a múltiplos monitores e testes automatizados. O alerta de hidratação inclui sprites pixel art licenciados, animação de entrada, teste manual pela bandeja e som nativo opcional do Windows. As correções mandatórias da diretriz foram aplicadas em `Domain.cs`, `MainWindow.xaml.cs` e `WaterPetOverlay.xaml`. A validação final deve ser executada em runner ou máquina Windows.

## Achados prioritários

| ID | Severidade | Achado | Impacto | Correção aplicada ou recomendada |
|---|---|---|---|---|
| S-01 | Alta | A especificação chama o fluxo de “triagem” e prescreve exercícios com base em dor. | Pode induzir diagnóstico, agravar lesão e criar risco regulatório e de responsabilidade. | O MVP usa linguagem de bem-estar, inclui aviso para interromper em caso de dor e recomenda avaliação profissional. Exercícios são suaves e não substituem orientação clínica. |
| S-02 | Alta | Perfil contém idade, peso, altura e queixas, todos dados potencialmente sensíveis. | Exposição local, cópia em backup, malware ou outro usuário do Windows. | Persistência em `%LocalAppData%/CozyBreak`, proteção DPAPI no usuário atual, arquivo temporário com substituição atômica, permissões herdadas do perfil do usuário e ausência de telemetria. O MVP não persiste IMC. |
| S-03 | Alta | “Zero ameaças” via VirusTotal é uma afirmação incorreta. | Falsa garantia de segurança e possível vazamento do binário para serviço externo. | Pipeline deve publicar hash e resultado do scan com data, nunca uma garantia absoluta. O upload ao VirusTotal deve ser opt-in e nunca conter dados do usuário. |
| S-04 | Alta | Assinatura digital foi citada sem certificado, gestão de segredo ou política de trust. | Binário não autenticado, risco de supply chain e falha operacional no CI. | Assinatura deve ocorrer apenas em runner protegido, com certificado armazenado como segredo. O MVP não embute certificado. |
| S-05 | Média | `Topmost=True` e notificações sem limites podem interromper reuniões ou cobrir telas. | Experiência invasiva e possível bloqueio de interação. | Modo apresentação, adiamento, limite de uma notificação por vez e janela discreta. O alerta não usa captura de teclado nem bloqueia o mouse inteiro. |
| S-06 | Média | `DispatcherTimer` não trata suspensão, troca de dia, jornada ou idle. | Alertas atrasados, duplicados ou fora do horário. | O MVP evita múltiplos overlays, permite adiamento e modo apresentação. A versão seguinte deve integrar horário de trabalho e detecção de sessão bloqueada/idle. |
| S-07 | Média | O cálculo `peso × 35` pode ser interpretado como recomendação clínica. | Excesso de água, inadequação para condições médicas e falsa precisão. | A meta é rotulada como estimativa de bem-estar, pode ser alterada e não é mostrada como orientação médica. Incluir exclusão explícita para condições renais/cardiacas na UI final. |
| S-08 | Média | Dependência de `NotifyIcon` não existe no WPF puro. | Projeto não compila com a configuração original. | O `.csproj` habilita `UseWindowsForms`; o tray é isolado no serviço de infraestrutura. |
| S-09 | Baixa | Assets visuais de terceiros exigem atribuição e incorporação correta. | Resultado visual inconsistente ou risco de licença. | Sprites do mascote estão em `src/CozyBreak/Assets/Pet`, embutidos no executável e atribuídos em `CREDITS.md`. |
| S-10 | Média | Single-file não significa necessariamente zero detecção pelo antivírus. | Falsos positivos e bloqueios em empresas. | Releases devem oferecer hash, SBOM, origem reprodutível, assinatura e instruções de verificação. Não executar código baixado sem validação. |

## Decisões de segurança

O aplicativo não possui servidor, conta, API, analytics, telemetria ou download de conteúdo. O arquivo de perfil é escrito somente em uma pasta privada do usuário. A gravação usa arquivo temporário e `File.Replace` quando disponível, reduzindo corrupção em desligamento abrupto. A desserialização rejeita valores inválidos por validação de domínio antes de serem usados.

O MVP não grava o índice de massa corporal. O requisito original de salvar `imcAjustado` é desnecessário para o funcionamento e aumenta a exposição de dado derivado. O IMC também não deve ser usado para classificar risco individual sem contexto clínico.

## Limites funcionais do MVP

O MVP inclui onboarding, edição de configurações, cálculo da meta estimada, lembrete de água, pausa ativa, modo apresentação por uma hora, adiamento, encerramento pelo tray e recomendações filtradas pelas áreas de desconforto. A interface é propositalmente simples para manter o núcleo auditável.

Não estão incluídos nesta primeira versão: suporte completo a múltiplos monitores, detecção de atividade do teclado/mouse, horário de expediente, instalador, atualização automática, assinatura de código, acessibilidade auditada e testes em cada versão suportada do Windows.

## Checklist antes do primeiro release

1. Compilar em Windows com `dotnet restore`, `dotnet test` e `dotnet publish`.
2. Ativar análise de código, auditoria de dependências e geração de SBOM.
3. Assinar o executável em ambiente protegido e publicar SHA-256.
4. Testar suspensão, retomada, troca de fuso, mudança de horário, múltiplos monitores e encerramento forçado.
5. Fazer revisão de privacidade, retenção e exclusão do perfil.
6. Revisar o texto com profissional de ergonomia/saúde ocupacional.
7. Testar com Windows Defender e solução corporativa real; não prometer “zero ameaças”.
8. Criar política de suporte e canal de vulnerabilidades no GitHub.

## Execução em Windows

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet publish src/CozyBreak/CozyBreak.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64
Get-FileHash artifacts/win-x64/CozyBreak.exe -Algorithm SHA256
```

O executável publicado será portátil, mas a compatibilidade depende do Windows suportado e da política de execução da empresa. A publicação não substitui assinatura digital.

## Referências

[1]: https://learn.microsoft.com/dotnet/core/deploying/single-file/overview "Single-file deployment overview"

[2]: https://learn.microsoft.com/windows/win32/secauthn/cryptprotectdata "Data protection API"

[3]: https://owasp.org/www-project-application-security-verification-standard/ "OWASP Application Security Verification Standard"

[4]: https://www.virustotal.com/gui/home/upload "VirusTotal upload and analysis service"

[5]: https://www.who.int/publications/i/item/9789240071508 "WHO guidelines on physical activity and sedentary behaviour"

# Atualização do MVP — melhorias incorporadas

A segunda versão do MVP adiciona proteção local com DPAPI no escopo do usuário atual, migração do arquivo JSON legado, projeto de testes automatizados, auditoria de cobertura no CI, nomes de acessibilidade nos controles, exibição da pausa na tela ativa e script opcional de assinatura Authenticode. A exclusão do perfil está disponível na camada de armazenamento, mas ainda não possui comando exposto na interface.

A assinatura digital não pode ser executada automaticamente sem um certificado corporativo. O script `scripts/sign-release.ps1` exige um certificado instalado no runner Windows e falha se a assinatura não for validada. O certificado e a chave privada nunca devem ser armazenados no repositório.

A revisão clínica/ergonômica foi incorporada como uma restrição de produto e linguagem: os exercícios são descritos como movimentos suaves, o aplicativo se posiciona como ferramenta de bem-estar e o usuário é orientado a parar diante de sintomas. Isso **não substitui** uma revisão formal por profissional habilitado antes do uso corporativo.

A acessibilidade básica foi adicionada com `AutomationProperties.Name`, navegação por teclado e botão explícito de conclusão. A compatibilidade completa com leitores de tela e escalas DPI ainda precisa de teste manual no Windows.

O MVP usa a tela sob o cursor como tela ativa para a janela de pausa e respeita a área de trabalho útil. Isso cobre a base de múltiplos monitores, mas deve ser validado com diferentes escalas, orientação e monitores desconectados durante a execução.

A execução real em Windows continua pendente neste ambiente porque o SDK .NET e o WPF não estão disponíveis no Linux de desenvolvimento. O pipeline GitHub Actions agora executa restore, build, testes, publicação e auditoria em `windows-latest`.

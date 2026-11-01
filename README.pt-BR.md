<img src="icon.png" width="128" align="right" alt="Island UI">

# Island UI

**Sua ilha, bem à vista.**

[English](README.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [Русский](README.ru.md) · **Português (Brasil)** · [Español](README.es.md) · [Deutsch](README.de.md)

<sub>Esta página foi traduzida por máquina; a versão em inglês é a que vale.</sub>

![Island UI em jogo: o HUD, uma ficha de personagem, a lista de moradores, a criação e as configurações](O1.webp)

**Uma interface coerente e feita com cuidado para Mad Island.** Ela se apoia em alguns princípios, e cada um dos seus [200 recursos →](FEATURES.md) nasce deles:

- **[Construída do zero.](SHOWCASE.md#built-from-the-ground-up)** Não são mil curativos. O Island UI dá um passo atrás e refaz a interface por completo, do conceito ao código.
- **[Natural.](SHOWCASE.md#second-nature)** Tudo é apresentado do jeito que as pessoas absorvem as coisas naturalmente, então nada precisa de explicação. Os itens voam até você quando você os pega, e os atributos mostram o que significam em vez de serem números soltos.
- **[Jogar, não preencher papelada.](SHOWCASE.md#play-not-paperwork)** As tarefas chatas saem do caminho. Seu machado aparece quando você golpeia uma árvore, você bebe no poço sem clicar, e um só toque esvazia todos os baús ao redor nas suas bolsas.
- **[Uma só função.](SHOWCASE.md#one-job)** O Island UI é um projeto grande com um objetivo estreito: a interface. Ele fica de propósito na sua própria raia, para não atrapalhar o conteúdo do jogo nem seus outros mods. Suas facilidades respeitam o espírito do jogo, e os ajustes que mudam as regras ficam no Island Ex.

Este é um trabalho feito com carinho, e quero que fique realmente bom. Adoraria saber o que você acha, então me mande uma mensagem no [Discord](https://discord.com/users/1523362502224777439) ou [abra uma issue aqui](../../issues).

**[Veja em jogo, com vídeos →](SHOWCASE.md)**

## Instalação

Baixe `Island-UI-vX.Y.Z-installer.zip` em [Releases](../../releases), extraia em qualquer lugar e dê dois cliques em `install.bat`. Ele encontra o jogo pela Steam, adiciona o BepInEx só se o jogo ainda não tiver, e copia o mod. `uninstall.bat` remove tudo de novo.

<details><summary><b>Outros downloads, e desinstalar manualmente</b></summary>

| Download | Para quem |
|---|---|
| `Island-UI-vX.Y.Z-installer.zip` | **O mais fácil.** O instalador acima. Não mexe em outros mods nem em configurações, e não baixa nada. |
| `Island-UI-vX.Y.Z-with-Island-Ex-installer.zip` | O mesmo instalador, com o Island Ex junto. |
| `Island-UI-vX.Y.Z-with-BepInEx.zip` | Sem instalador: extraia na pasta do jogo (ao lado de `Mad Island.exe`). Já vem com o BepInEx 5.4.23.5. |
| `Island-UI-vX.Y.Z.zip` | Você já tem o BepInEx 5: extraia na pasta do jogo. |
| `Island-Ex-vX.Y.Z.zip` | Só o Island Ex: extraia na pasta do jogo. Precisa do BepInEx 5. |

A pasta do jogo costuma ser `C:\Program Files (x86)\Steam\steamapps\common\Mad Island` (na Steam: clique com o botão direito no jogo, *Gerenciar*, *Ver arquivos locais*). O Windows pode avisar sobre executar um script baixado; o instalador é texto simples e está em [`installer/`](installer).

Para desinstalar manualmente, apague `BepInEx/plugins/IslandUI` e `BepInEx/plugins/islandui-art` (e `BepInEx/plugins/IslandEx` para o Island Ex).

</details>

## Configurações

O menu do jogo tem uma entrada **Island UI** abaixo de *Option*. As mudanças valem na hora. O mod segue o idioma do jogo, ou o que você escolher: inglês, chinês simplificado, japonês, russo, português brasileiro, espanhol ou alemão.

<details><summary><b>As páginas, as traduções e o arquivo de configuração</b></summary>

As páginas vêm em grupos: Geral (o seu ponteiro e o idioma), Alvo, Combate, Avisos, Janelas, História e Atalhos. O **Island Ex** ganha a própria entrada enquanto estiver instalado.

<img src="SET.jpg" width="480" alt="A janela de configurações na página Combate">

As traduções são feitas por máquina, então correções são bem-vindas: abra uma issue, ou edite `BepInEx/plugins/IslandUI/Localization/<language>/strings.txt` (e `item-guide.tsv`, `story-choices.tsv`) e envie. O cabeçalho de cada arquivo explica como.

Desligar as partes maiores (a janela de arredores, a barra de ações, o painel do alvo, a lista de moradores, as fichas de personagem) fica em `BepInEx/config/madisland.islandui.cfg`.

Os ícones dos mods no canto inferior esquerdo da tela de título mostram que eles carregaram; passe o mouse para ver a versão, clique para desligar ou ligar o mod.

</details>

## Island Ex

<img src="island-ex.png" width="64" align="right" alt="Island Ex">

O Island UI joga pelas regras do jogo: muda como você vê o jogo e como chega às coisas, nunca o que você pode fazer. Ajustes que mudam as regras ficam em um mod pequeno e separado, o Island Ex, no mesmo lançamento. O Island UI não precisa dele.

<details><summary><b>O que o Island Ex faz</b></summary>

- **Filtro de efeitos**: desligue qualquer efeito visual que você prefira não ver.
- **Segurar para se soltar**: numa agarrada, segurar o botão de ataque continua batendo.
- **Fica na tela**: o jogo não minimiza mais com Alt+Tab ou com um clique em outro monitor.
- **Seco dentro de casa**: a chuva não cai mais dentro das casas.
- **Afastar a câmera**: afaste a câmera mais do que o jogo permite.
- **Guardar ferramentas quebradas**: uma ferramenta gasta fica na bolsa, quebrada, esperando um kit de reparo.

</details>

## Limitações

O Island UI é novo. A 0.9 refaz a maior parte da interface e foi jogada, não testada a fundo: espere arestas e um bug aqui e ali. Algumas telas do próprio jogo ainda não foram refeitas e continuam com a aparência e o funcionamento de sempre. Se algo quebrar, uma issue com o seu `BepInEx/LogOutput.log` anexado é o que mais ajuda.

Testado no branch `beta_open` da Steam, build 25095856, com BepInEx 5.4.23.5. Depois de uma atualização do jogo, procure em `BepInEx/LogOutput.log` por `Patch census [IslandUI]: ... all bound.` Se aparecer outra coisa, a atualização mudou algo e o mod precisa de um novo lançamento.

## Código-fonte

Cada commit aqui é um lançamento; as tags de versão batem com os zips dos lançamentos.

<details><summary><b>Como compilar</b></summary>

Para compilar, você precisa do .NET SDK e do jogo instalado (os projetos referenciam as DLLs dele):

```
dotnet build src/IslandUI/IslandUI.csproj -c Release
dotnet build src/IslandEx/IslandEx.csproj -c Release
```

A compilação copia a DLL para o `BepInEx/plugins` do jogo; adicione `-p:GameDir="D:\...\Mad Island"` para um jogo em outro lugar, ou `-p:NoDeploy=true` para compilar sem copiar. O Island UI também precisa de `art/*.png` em `BepInEx/plugins/islandui-art`.

</details>

## Contato

Bugs, ideias, ou quer reaproveitar algo? [Abra uma issue](../../issues), escreva para [maliekee@pm.me](mailto:maliekee@pm.me) ou me encontre no [Discord](https://discord.com/users/1523362502224777439).

## Obrigado pelo engajamento e pelo apoio

h1sagi, NoobInDaHause, Ramiyak, Ex0du5_2169, Dimastrelok, Yuuto, Cosmo, Abyss, NotTeaBut, Kringe, Kestraz

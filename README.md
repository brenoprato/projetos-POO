# Projetos — Programação Orientada a Objetos (POO)

Repositório destinado aos projetos desenvolvidos na disciplina de **Programação Orientada a Objetos (POO)** em **C# (.NET 10)** na **Universidade Federal de São Paulo (Unifesp)**.

## Projetos

| Projeto | Diretório | Descrição |
| --- | --- | --- |
| **Blackjack (21)** | [`blackjack/`](blackjack/) | Implementação do jogo de cartas Blackjack (21) com interface gráfica em **Raylib-cs**, aplicando encapsulamento, herança (`Dealer : Player`), associação, composição e sobrecarga de operadores. Veja os detalhes em [`blackjack/README.md`](blackjack/README.md). |
| **Pong (Tênis de Mesa)** | [`pong/`](pong/) | Implementação do jogo Pong com interface gráfica em **Raylib-cs**, estruturada com classes de domínio (`Ball`, `Paddle`, `Player`), componente de interface (`Button`) e loop de lógica em thread dedicada (`PongGame`). |

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Ambiente com suporte gráfico para execução da biblioteca **Raylib-cs** (`8.1.0`)

## Como executar

Acesse o diretório do projeto desejado e execute com o .NET CLI:

```bash
# Exemplo: Blackjack
cd blackjack
dotnet run
```

```bash
# Exemplo: Pong
cd pong
dotnet run
```

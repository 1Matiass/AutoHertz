# AutoHertz

**Troque a taxa de atualização (Hz) do seu notebook automaticamente para economizar bateria.**
App leve (~50 KB), sem dependências, para Windows 10/11. Ele detecta sozinho as taxas que a sua tela suporta (60, 75, 90, 144, 165, 240…) e alterna entre elas de forma automática — ou você troca manualmente com um clique.

<p align="center">
  <img src="docs/screenshot.png" alt="AutoHertz" width="380">
</p>

## ✨ Recursos

- **Leitura em tempo real** — mostra em quantos Hz a tela está agora e até quanto ela vai.
- **Troca manual instantânea** — botões com todas as taxas suportadas; clicou, mudou.
- **3 modos automáticos:**
  - 🔋 **Economia de energia** — cai para 60 Hz só quando o Windows entra no modo economia.
  - 🔌 **Fora da tomada** — cai para 60 Hz sempre que estiver na bateria.
  - 🧹 **Remover** — desativa e limpa tudo.
- **Sem fricção** — inicia com o Windows, roda escondido (sem janela/console), **não pede admin**.
- **Portátil** — um único `.exe`. Copie para qualquer notebook e use.

## 🚀 Como usar

1. Baixe o `AutoHertz.exe`.
2. Clique duas vezes.
3. Escolha um modo (ou clique numa taxa para trocar na hora). Pronto — configura tudo sozinho.

Para desligar: abra de novo e escolha **Remover automação**.

### Linha de comando (instalação em lote)

```bat
AutoHertz.exe --install 1   :: modo economia de energia
AutoHertz.exe --install 2   :: modo fora da tomada
AutoHertz.exe --uninstall   :: remove tudo
```

## 🛠️ Como funciona

- Escrito em **C# (WinForms)** e compilado com o `csc.exe` que **já vem no Windows** (.NET Framework 4) — sem SDK.
- Um vigia oculto escuta eventos de energia (`WM_POWERBROADCAST` / `RegisterPowerSettingNotification`) e ajusta a taxa via `ChangeDisplaySettings`.
- Inicia pelo `HKCU\...\Run` (por usuário, sem privilégios de administrador).
- Instala em `C:\Users\<você>\AutoHertz\`.

### Compilar do código

```bat
cd src
build.bat
```

## 📇 Contato

WhatsApp: **(74) 99988-7338**

## 📄 Licença

MIT.

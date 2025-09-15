# SFAgent_TRANSP

## 🚀 Sobre o Projeto

Serviço responsável por consumir dados da tabela de **transportadora** no SAP (via HANA) e criar ou atualizar registros correspondentes na **Salesforce** por meio de API REST.

- Recupera dados do SAP através do drive ODBC.  
- Envia registros formatados para objetos customizados do Salesforce.  
- Mantém logs detalhados em arquivo e no Event Viewer do Windows.  

---

## ⚙️ Estrutura do Projeto

```text
MappingService
│
├─ Config
│   ├─ ConfigCred.cs         # Centraliza credenciais do Salesforce (ClientId e ClientSecret)
│   └─ ConfigUrls.cs         # Centraliza URLs de autenticação e endpoints da API
│
├─ Salesforce
│   ├─ SalesforceAuth.cs     # Autenticação no Salesforce (OAuth2 Client Credentials)
│   └─ SalesforceApi.cs      # Chamadas de API (GET/POST para objetos no Salesforce)
│
├─ Sap
│   └─ SapConnector.cs       # Conexão ODBC com SAP HANA (ex.: SELECT TOP 1 * FROM OINV)
│
├─ Services
│   └─ Service1.cs           # Orquestra o fluxo (SAP → Salesforce) e ciclo de vida do Windows Service
│
├─ Utils
│   └─ Logger.cs             # Grava logs em arquivo (C:\Logs\MappingService) e Event Viewer
│
├─ Program.cs                # Entry point do serviço (ServiceBase.Run)
└─ ProjectInstaller.cs       # Define instalação do serviço no Windows (nome, descrição, conta de execução)
```

---

## 🛠️ Pré-requisitos

- Visual Studio 2019/2022  
- .NET Framework 4.8  
- Driver ODBC do SAP HANA (HDBODBC) instalado  
- Acesso ao banco de dados SAP (usuário e senha válidos)  
- Credenciais Salesforce (ClientId e ClientSecret do Connected App)  

---

## 🔧 Instalação do Serviço

Compile o projeto em **Debug** e vá até a pasta `bin\Debug` (ou `bin\x64\Debug` se estiver usando 64 bits).  

### ▶️ Instalar o serviço
**Any CPU / x86**

Após abrir o diretório de Compilação:

###Exemplo###
<img width="1353" height="695" alt="image" src="https://github.com/user-attachments/assets/adb94611-e05d-4e63-9fa3-f213cc2d3974" />

Irá copiar esse diretório e após copiar irá abrir o PS Admin apertando "**Windowns**+**X**", e clicar nessa opção:
<img width="599" height="770" alt="image" src="https://github.com/user-attachments/assets/e96dd9f5-09fc-4700-8f0f-a4d56ec93d0f" />

Após isso basta digitir cd e abrir aspas duplas e colar o diretório que copio e depois fechar aspas duplas e dar enter para entrar nesse diretório, e ficará assim:
<img width="1511" height="308" alt="image" src="https://github.com/user-attachments/assets/3e45212b-8f9b-4755-b38a-9b5e70820aca" />

ai Então irá copiar os diretórios abaixo de acordo com a estrutura em bits da sua compilação, e mudar o nome do seu executavel também.

```powershell
& "C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe" NOME DO SEU EXECUTAVEL.exe
```

**x86**
```powershell
& "C:\Windows\Microsoft.NET\Framework86\v4.0.30319\InstallUtil.exe" NOME DO SEU EXECUTAVEL.exe
```

**x64**
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe" NOME DO SEU EXECUTAVEL.exe
```

Após mudar o nome e copiar e colar no ps ele irá efetuar a instalação, assim:

<img width="1502" height="716" alt="image" src="https://github.com/user-attachments/assets/c7eba4b7-147e-403c-a7bc-b89e031c369f" />


### 🗑️ Desinstalar o serviço

Para desisntalar seu serviço, basta seguir o mesmo processo de Instalação, entrando no diretório de compilação (mesmo diretório que o serviço esta instalado) e acessar via powershell admin, após isso so colar o end abaixo conforme a arquitetura em bits do seu projeto, e nâo esqueça de mudar o nome.

```powershell
& "C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe" /u NOME DO SEU EXECUTAVEL.exe
```

**x86**
```powershell
& "C:\Windows\Microsoft.NET\Framework86\v4.0.30319\InstallUtil.exe" /u NOME DO SEU EXECUTAVEL.exe
```

**x64**
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe" /u NOME DO SEU EXECUTAVEL.exe
```

Ficará assim:

<img width="1478" height="566" alt="image" src="https://github.com/user-attachments/assets/1dd13b0f-4d8d-4d8e-95f7-156ae49c878e" />

---

## 📌 Funcionamento do Serviço

1. **Start (`OnStart`)**
   - Obtém o token de acesso do Salesforce (OAuth2).  
   - Conecta ao SAP HANA via ODBC.  
   - Loga o resultado da query (sucesso ou falha). 
   - Envia um `POST` de teste para o objeto `CA_Transportadora__c` no Salesforce.  
   - Registra tudo em log (`C:\SFAgent\Logs - Transportadora.txt`) e no **Event Viewer**.  

2. **Stop (`OnStop`)**
   - Escreve no log que o serviço foi interrompido.  

---

## 📝 Logs

- Local: `C:\SFAgent\Logs - Transportadora.txt`  
- Event Viewer: **Application → Source: SFAgent** (Apenas Erros)  


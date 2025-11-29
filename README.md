# Building AI Agents from Scratch with Azure AI Foundry & C# 🤖

This repository contains a collection of **Intelligent AI Agents** built using **.NET 8 (C#)** and **Azure OpenAI Service**. 

Moving beyond simple chatbots, these projects demonstrate advanced **Agentic Patterns** such as Function Calling, RAG (Retrieval-Augmented Generation), Text-to-SQL, Multi-Modal processing, and Multi-Agent Orchestration.

## 🚀 Tech Stack
* **Language:** C# (.NET 8.0 Console Apps)
* **AI Model:** GPT-4o & text-embedding-ada-002
* **SDK:** `Azure.AI.OpenAI`
* **Tools:** SQLite, PdfPig, OpenMeteo (Simulated), Vector Search logic.

---

## 📂 Project Modules

### 1. DevOps Cloud Infrastructure Agent ☁️
* **Goal:** Automate infrastructure provisioning and cost estimation.
* **Mechanism:** The agent converts natural language requests (e.g., *"Create a VM in West Europe"*) into ready-to-deploy **Terraform** code.
* **Key Feature:** Integrates with **Azure Retail Prices API** to fetch real-time pricing and embeds estimated monthly costs directly into the generated code.
* **Pattern:** Function Calling (Tools).

### 2. SQL Database Detective 🔍
* **Goal:** Enable non-technical users to query databases using natural language.
* **Mechanism:** Uses an In-Memory **SQLite** database populated with sales data. The agent translates user questions into valid SQL queries, executes them, and interprets the results.
* **Key Feature:** Prevents hallucination by strictly adhering to the injected database schema.
* **Pattern:** Text-to-SQL / Code Interpreter.

### 3. Smart HR Resume Screener 📄
* **Goal:** Automate the recruitment process for technical roles.
* **Mechanism:** Parses PDF resumes using OCR, evaluates candidates against a specific Job Description (e.g., DevOps Engineer), and calculates a match score.
* **Key Feature:** Implements **Decision Logic**:
    * *Score > 70:* Checks calendar availability and drafts an invite email.
    * *Score < 70:* Generates a polite rejection summary.
* **Pattern:** Multi-Modal Processing & Logic Workflow.

### 4. RAG-based Technical Support 📚
* **Goal:** Create a knowledgeable support assistant grounded in internal documentation.
* **Mechanism:** Uses **Vector Embeddings (`text-embedding-ada-002`)** and **Cosine Similarity** to search through an internal Knowledge Base (KB).
* **Key Feature:** **Grounding.** The agent only answers based on retrieved context and cites the source ID (e.g., `[Source: DOC-001]`). It explicitly refuses to answer if information is missing in the docs.
* **Pattern:** RAG (Retrieval-Augmented Generation) & Vector Search.

### 5. Multi-Agent Travel Orchestrator 🌍
* **Goal:** Plan complex trips by coordinating multiple specialized agents.
* **Mechanism:** A **Manager Agent (Orchestrator)** breaks down the user request and delegates tasks to sub-agents.
    * **Weather Agent:** Predicts weather conditions based on historical climate data using a dedicated LLM personality.
    * **City Guide Agent:** Provides tourist attraction recommendations.
* **Key Feature:** Demonstrates **Agent-to-Agent communication**, where one AI model invokes another to solve a sub-problem.
* **Pattern:** Multi-Agent Orchestration / ReAct.

---

## 🛠️ How to Run

1.  Clone the repository:
    ```bash
    git clone [https://github.com/WarLock54/Azure-AI-Agents.git](https://github.com/WarLock54/Azure-AI-Agents.git)
    ```
2.  Navigate to the specific project folder (e.g., `MultiAgentSystem`).
3.  Open the solution in **Visual Studio** or **VS Code**.
4.  Update the `Program.cs` file with your Azure OpenAI credentials:
    ```csharp
    string AZURE_OPENAI_ENDPOINT = "YOUR_ENDPOINT";
    string AZURE_OPENAI_KEY = "YOUR_KEY";
    ```
5.  Run the application:
    ```bash
    dotnet run
    ```

## 👨‍💻 Author

**Onur** Software Engineer | Cloud & DevOps Enthusiast  
[GitHub Profile](https://github.com/WarLock54)

---
*Note: This project is for educational purposes to demonstrate the capabilities of Azure AI Foundry.*

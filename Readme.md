# 👀 BG3 Chat – RAG with Qdrant + OpenAI

This is a test project intended to experiment with Retrieval-Augmented Generation (RAG) techniques in .NET, using Qdrant as a vector store and OpenAI for embeddings.

A .NET console app that answers questions about Baldur's Gate 3 items, based on Retrieval-Augmented Generation (RAG) using OpenAI embeddings and Qdrant as a semantic vector store.

Excel file used as data source comes from a [post on the Baldur's Gate 3 subreddit](https://www.reddit.com/r/BaldursGate3/comments/16acy9l/bg3_cheat_sheet_for_items_in_each_act_spoilers/).

---

## 💠 Requirements

* [Docker](https://www.docker.com/products/docker-desktop)
* (Optional) [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download) – only needed if you want to run the project outside Docker
* A valid OpenAI API key (`text-embedding-3-small` and `gpt-4.1-nano`)

---

## ⚙️ Environment Setup

Create a `.env` file in the project root with the following content:

```
AI__OPENAI__APIKEY=sk-xxxxx
```

---

## ▶️ Running with Docker Compose

### ⚖️ Detached mode (in background)

```bash
docker compose up -d --build
```

Then manually attach to the container to use `Console.ReadLine()`:

```bash
docker attach bg3chat
```

To detach from the container without stopping it:

```
Ctrl + P, Ctrl + Q
```

### 🚀 Attached mode (direct)

```bash
docker compose up --build
```

In this mode, you'll see the output directly and can provide input if your terminal supports it.

---

## 📝 License

For personal/educational use only. All BG3 assets belong to their respective owners.

```sql

-- ============================================================
-- RAG AI API - PostgreSQL Database Script
-- ============================================================

-- Enable pgvector
CREATE EXTENSION IF NOT EXISTS vector;


-- ============================================================
-- 1. KNOWLEDGE BASES
-- ============================================================

CREATE TABLE IF NOT EXISTS knowledge_bases
(
    "Id" UUID NOT NULL,
    "TenantId" UUID NOT NULL,
    "Name" VARCHAR(1000) NOT NULL,
    "Description" TEXT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedOn" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_knowledge_bases"
        PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS
    "IX_knowledge_bases_TenantId_Name"
ON knowledge_bases ("TenantId", "Name");


-- ============================================================
-- 2. DOCUMENTS
-- ============================================================

CREATE TABLE IF NOT EXISTS documents
(
    "Id" UUID NOT NULL,
    "TenantId" UUID NOT NULL,
    "FileName" VARCHAR(1000) NOT NULL,
    "ContentType" VARCHAR(1000) NOT NULL,
    "FileSize" BIGINT NOT NULL,
    "StoragePath" TEXT NULL,
    "ContentHash" TEXT NULL,
    "Version" INTEGER NOT NULL DEFAULT 1,
    -- Stored as string because of HasConversion<string>()
    "Status" VARCHAR(30) NOT NULL DEFAULT 'Pending',
    "ErrorMessage" TEXT NULL,
    "CreatedOn" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ProcessedOn" TIMESTAMPTZ NULL,
    "KnowledgeBaseId" UUID NOT NULL,
    CONSTRAINT "PK_documents"
        PRIMARY KEY ("Id"),
    CONSTRAINT "FK_documents_knowledge_bases_KnowledgeBaseId"
        FOREIGN KEY ("KnowledgeBaseId")
        REFERENCES knowledge_bases ("Id")
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS
    "IX_documents_TenantId_KnowledgeBaseId_ContentHash"
ON documents
(
    "TenantId",
    "KnowledgeBaseId",
    "ContentHash"
);


-- ============================================================
-- 3. DOCUMENT CHUNKS
-- ============================================================

CREATE TABLE IF NOT EXISTS document_chunks
(
    "Id" UUID NOT NULL,
    "TenantId" UUID NOT NULL,
    "ChunkIndex" INTEGER NOT NULL,
    "Content" TEXT NOT NULL,
    "Heading" TEXT NULL,
    "TokenCount" INTEGER NOT NULL,
    "PageNumber" INTEGER NOT NULL,
    "MetadataJson" TEXT NULL,
    "EmbeddingModel" VARCHAR(200) NOT NULL,
    "EmbeddingDimensions" INTEGER NOT NULL,
    -- pgvector
    "Embedding" VECTOR(384) NOT NULL,
    "CreatedOn" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "DocumentId" UUID NOT NULL,
    CONSTRAINT "PK_document_chunks"
        PRIMARY KEY ("Id"),
    CONSTRAINT "FK_document_chunks_documents_DocumentId"
        FOREIGN KEY ("DocumentId")
        REFERENCES documents ("Id")
        ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS
    "IX_document_chunks_DocumentId_ChunkIndex"
ON document_chunks
(
    "DocumentId",
    "ChunkIndex"
);


-- ============================================================
-- 4. HNSW VECTOR INDEX
-- ============================================================

CREATE INDEX IF NOT EXISTS
    "IX_document_chunks_Embedding"
ON document_chunks
USING hnsw
(
    "Embedding" vector_cosine_ops
)
WITH
(
    m = 16,
    ef_construction = 64
);


-- ============================================================
-- 5. OPTIONAL: TENANT + DOCUMENT LOOKUP INDEX
-- ============================================================

CREATE INDEX IF NOT EXISTS
    "IX_document_chunks_TenantId_DocumentId"
ON document_chunks
(
    "TenantId",
    "DocumentId"
);

```

```text
                         ┌──────────────────────┐
                         │      Client/UI       │
                         └──────────┬───────────┘
                                    │
                     ┌──────────────▼──────────────┐
                     │       .NET 10 Web API       │
                     │ Authentication / Validation │
                     └───────┬──────────────┬──────┘
                             │              │
                    INGESTION│              │QUERY
                             │              │
              ┌──────────────▼───┐     ┌────▼──────────────┐
              │ Document Parser   │    │         Query Embedding   │
              │ PDF/DOCX/TXT/MD   │    │         BGE-small-en-v1.5 │
              └──────────┬────────┘    └────────┬─────────┘
                         │                      │
                  ┌──────▼──────┐        ┌──────▼─────────┐
                  │   Chunker   │               │ Vector Search  │
                  │ Token-aware │               │   PostgreSQL   │
                  └──────┬──────┘           │   + pgvector   │
                         │                   └──────┬─────────┘
                  ┌──────▼──────┐               │
                  │  Embedding  │                   │ Top-K
                  │    Model    │                   │
                  └──────┬──────┘                ┌──────▼─────────┐
                         │                      │    Reranker     │
                         ▼                      │ Cross Encoder   │
              ┌────────────────────┐             └──────┬─────────┘
              │ PostgreSQL         │                    │
              │ Documents          │                    │
                  │ Chunks             │                │
                  │ Embeddings         │                │
                  └────────────────────┘         ┌──────▼──────────┐
                                                 │ Context Builder │
                                                 └──────┬──────────┘
                                                        │
                                                 ┌──────▼──────────┐
                                                 │ Prompt Template │
                                                 └──────┬──────────┘
                                                        │
                                                 ┌──────▼──────────┐
                                                 │ Generator LLM   │
                                                 │ Ollama / OpenAI │
                                                 │ / Azure OpenAI │
                                                 └──────┬──────────┘
                                                        │
                                                 ┌──────▼──────────┐
                                                 │ Answer + Sources│
                                                 └─────────────────┘


```

```text
                 ┌───────────────┐
                 │    Document   │
                 └───────┬───────┘
                         ↓
                 ┌───────────────┐
                 │ Text Extractor│
                 └───────┬───────┘
                         ↓
                 ┌───────────────┐
                 │ Text Normalize│
                 └───────┬───────┘
                         ↓
                 ┌───────────────┐
                 │    Chunker    │
                 └───────┬───────┘
                         ↓
                ┌─────────────────┐
                │ Chunks 1..N     │
                └────────┬────────┘
                         ↓
                ┌─────────────────┐
                │ Embedding Model │
                └────────┬────────┘
                         ↓
                ┌─────────────────┐
                │ PostgreSQL      │
                │ + pgvector      │
                └─────────────────┘

```

```text
AskAsync()
   │
   ├── Embedding
   │
   ├── Vector Search
   │
   ├── Reranking
   │
   ├── Context Building
   │
   ├── Prompt Construction
   │
   └── LLM Generation

```

```text
                  INGESTION
                      │
                      ▼
               Upload Document
                      │
                      ▼
              Store Original File
                      │
                      ▼
                Extract Text
                      │
                      ▼
              Normalize Text
                      │
                      ▼
             Structure Detection
                      │
                      ▼
              Semantic Chunking
                      │
                      ▼
              Generate Embedding
                      │
                      ▼
             PostgreSQL/pgvector
                      │
                      │
══════════════════════╪════════════════════════════
                      │
                     QUERY
                      │
                      ▼
                 User Query
                      │
                      ▼
              Query Transformation
                      │
                      ▼
                Query Embedding
                      │
                      ▼
             Hybrid Vector Search
                      │
                      ▼
                  Top 20
                      │
                      ▼
                   Reranker
                      │
                      ▼
                   Top 5
                      │
                      ▼
                Context Builder
                      │
                      ▼
              RAG Prompt Template
                      │
                      ▼
                   LLM
                      │
                      ▼
               Answer + Sources

```

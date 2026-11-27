# GraphRAG Service

Python/FastAPI service that powers search, product recommendations and conversational Q&A for the 3D-printing catalog. It maintains two complementary indexes of the product catalog and combines them at query time:

| Store | Role |
|-------|------|
| **Qdrant** | Semantic layer: dense embedding per product + filterable payload |
| **Neo4j** | Graph layer: products, categories, materials, printers, attributes and typed product-to-product relations |

Everything model-related runs locally (Ollama for embeddings and LLMs, a Hugging Face cross-encoder for reranking). There are no external API calls.

- Service / package name: `graphrag-service` (version `0.1.0`)
- HTTP port: container `8100` (host port is `GRAPHRAG_API_PORT`, default `8100` in `docker/template.env`)
- Two runtime roles from the same image: an **API** (`SERVICE_MODE=api`) and an **ingestion worker** (`SERVICE_MODE=worker`)

---

## Table of contents

1. [Role in the system](#1-role-in-the-system)
2. [Tech stack](#2-tech-stack)
3. [Project layout](#3-project-layout)
4. [HTTP API](#4-http-api)
5. [Messaging (RabbitMQ)](#5-messaging-rabbitmq)
6. [Data storage](#6-data-storage)
7. [Retrieval and generation pipelines](#7-retrieval-and-generation-pipelines)
8. [Configuration](#8-configuration)
9. [Observability](#9-observability)
10. [Running locally](#10-running-locally)
11. [Testing](#11-testing)
12. [Known gaps and inconsistencies](#12-known-gaps-and-inconsistencies)

---

## 1. Role in the system

```
                         publishes product.* events
 Catalog service  ─────────────────────────────────►  RabbitMQ  (topic exchange "catalog-exchange")
                                                           │
                                                           │ queue "graphrag-ingestion"
                                                           ▼
                                                   graphrag-worker  ──► Qdrant   (vectors + payload)
                                                   (python -m                  ──► Neo4j    (graph)
                                                    app.workers.consumer)      ──► Ollama   (embeddings)

 Storefront ──► API Gateway ──/graphrag/*──► graphrag-api :8100 ──► Qdrant / Neo4j / Ollama (embed + LLM)
                                                                      └─► BAAI cross-encoder reranker (in-process)
```

| Related service | Relationship |
|-----------------|--------------|
| **Catalog** | Source of truth. Publishes the `product.*` events on `catalog-exchange` that this service consumes. GraphRAG never calls Catalog over HTTP and never writes back. |
| **RabbitMQ** | Transport for catalog events (consume only; GraphRAG publishes nothing). |
| **API Gateway** | Exposes the API to clients under the `/graphrag` prefix (see [Gateway exposure](#gateway-exposure)). |
| **Storefront** | Calls `POST /graphrag/api/search`, `/graphrag/api/chat` and `/graphrag/api/recommend` through the gateway (`Storefront/src/lib/api/search.ts`, no auth token sent). |
| **Qdrant / Neo4j / Ollama** | Datastores and model server owned by the compose stack. |
| **Jaeger** | OTLP gRPC trace sink (API process only, see [Observability](#9-observability)). |

---

## 2. Tech stack

| Area | Detail |
|------|--------|
| Language | Python `>=3.11` (Docker image: `python:3.11-slim`) |
| Web | FastAPI `>=0.115`, Uvicorn (`uvicorn[standard]`), Pydantic `>=2.9`, `pydantic-settings` `>=2.5` |
| Messaging | `aio-pika` `>=9.4` (`connect_robust`) |
| Vector DB client | `qdrant-client` `>=1.12` (synchronous `QdrantClient`) |
| Graph DB client | `neo4j` `>=5.25` (async driver) |
| LLM / embeddings client | `ollama` `>=0.3` (`AsyncClient`) |
| Reranking | `sentence-transformers` `>=3.0` `CrossEncoder` (pulls in `torch`); only listed in `requirements.txt` |
| Optional embedding backend | `FlagEmbedding` `>=1.2` (`BGEM3FlagModel`), not wired by default |
| Logging | `structlog` `>=24.4` |
| Tracing | `opentelemetry-*` (API, SDK, OTLP gRPC exporter, FastAPI / httpx / aio-pika / logging instrumentation) |
| Other declared deps | `langchain`, `langchain-community`, `langchain-ollama`, `httpx`, `tenacity`, `numpy` (declared, but `langchain*` and `tenacity` are not imported anywhere in `app/`) |
| Tooling | `pytest`, `pytest-asyncio` (`asyncio_mode = "auto"`), `pytest-cov`, `ruff` (line length 100, target `py311`) |

### Models

| Purpose | Default | Setting | How it is served / loaded |
|---------|---------|---------|---------------------------|
| Dense embeddings (1024-d) | `bge-m3` | `EMBEDDING_MODEL` | Ollama `embed` endpoint via `OllamaEmbeddingProvider`. Must be pulled into Ollama (`ollama pull bge-m3`). |
| Reranker (cross-encoder) | `bge-reranker-v2-m3` | `RERANKER_MODEL` | **Not** served by Ollama. Loaded in-process by `sentence_transformers.CrossEncoder` from Hugging Face as `BAAI/<RERANKER_MODEL>` (`max_length=512`, CUDA + fp16 if a GPU is available, else CPU fp32). Downloaded on first use (lazy: first search/recommend/chat request). |
| Fast LLM | `gemma3:4b` | `GENERATION_MODEL` | Ollama chat. Used for intent classification and query rewriting (`temperature=0`, `num_predict=256`). |
| Quality LLM | `gemma3:12b` | `GENERATION_MODEL_LARGE` | Ollama chat. Used for chat answer synthesis (`temperature=0.3`, `num_predict=1024`). |

---

## 3. Project layout

```
GraphRAG/
├── app/
│   ├── api/
│   │   ├── main.py              FastAPI app, lifespan (logging, telemetry, ensure Qdrant collection + Neo4j constraints)
│   │   ├── dependencies.py      lru_cache'd singletons + pipeline factories (Depends)
│   │   ├── schemas.py           Pydantic request/response models
│   │   └── routes/              search.py, recommend.py, chat.py, health.py
│   ├── core/                    settings.py, logging.py (structlog), telemetry.py (OpenTelemetry)
│   ├── domain/                  ProductDocument, AttributeBucket, SearchQuery/SearchIntent, RetrievalResult/SearchResponse
│   ├── embeddings/              EmbeddingProvider (ABC), OllamaEmbeddingProvider, BGEM3EmbeddingProvider, service.py
│   ├── ingestion/               transformers.py, attribute_classifier.py, handlers/ (one per event)
│   ├── vector/                  qdrant_client.py (collection + indexes), vector_store.py (upsert/search)
│   ├── graph/                   neo4j_client.py (driver + constraints), cypher_queries.py, graph_repository.py
│   ├── retrieval/               HybridSearchStrategy, GraphExpansionStrategy, merge_results, build_qdrant_filters
│   ├── ranking/                 Reranker (cross-encoder), apply_business_signals
│   ├── generation/              LLMClient, IntentClassifier, QueryRewriter, AnswerGenerator
│   ├── pipeline/                SearchPipeline, RecommendationPipeline, ChatPipeline
│   ├── workers/                 consumer.py (EventConsumer + main), __main__.py
│   └── utils/                   normalize_uuid / truncate_text / clean_text (currently unused)
├── prompts/                     answer_generation.txt, intent_classification.txt, query_rewrite.txt
├── scripts/                     seed.py (sample catalog via gateway), reindex.py (Neo4j -> Qdrant)
├── tests/                       unit/ and integration/, conftest.py
├── docker/                      Dockerfile, entrypoint.sh
├── pyproject.toml
└── requirements.txt             (used by the Dockerfile; also includes sentence-transformers)
```

Layering: `routes -> pipelines -> (retrieval | ranking | generation) -> (vector | graph | embeddings)`. Ingestion handlers use `transformers -> ProductDocument -> VectorStore + GraphRepository`.

---

## 4. HTTP API

Served by `app.api.main:app` on port `8100`. Interactive docs: `/docs` (Swagger UI), `/redoc`, `/openapi.json`. No authentication is performed by the service.

| Method | Path | Request model | Response model | Description |
|--------|------|---------------|----------------|-------------|
| `GET` | `/health` | none | `HealthResponse` | Connectivity check for Qdrant, Neo4j, Ollama |
| `POST` | `/api/search` | `SearchRequest` | `SearchResponse` | Full search pipeline |
| `POST` | `/api/recommend` | `RecommendRequest` | `SearchResponse` | Products similar to a seed product |
| `POST` | `/api/chat` | `ChatRequest` | `ChatResponse` | Search + grounded LLM answer |

Status codes: `200` on success; `422` for request-validation failures (FastAPI default); `500` for any unhandled error (e.g. Qdrant, Neo4j or Ollama unreachable, LLM failure). Routes have no custom error handling, and `/health` itself always returns `200` (see below).

### `POST /api/search`

Request (`SearchRequest`):

| Field | Type | Constraints | Default |
|-------|------|-------------|---------|
| `query` | string | 1-500 chars, required | |
| `filters` | object | see [Filters](#search-filters) | `{}` |
| `limit` | int | 1-100 | `10` |

```json
{
  "query": "articulated dragon for ender 3",
  "filters": { "materials": ["PLA", "PETG"], "price_max": 10, "min_rating": 4.0 },
  "limit": 5
}
```

Response (`SearchResponse`, also used by `/api/recommend`; `answer` is declared but never populated by these two endpoints):

```json
{
  "results": [
    {
      "product_id": "550e8400-e29b-41d4-a716-446655440001",
      "name": "Articulated Dragon",
      "slug": "articulated-dragon",
      "score": 0.8731,
      "vector_score": 0.7412,
      "graph_score": 0.0,
      "rerank_score": 0.9650,
      "metadata": {
        "product_id": "550e8400-e29b-41d4-a716-446655440001",
        "name": "Articulated Dragon",
        "slug": "articulated-dragon",
        "price": 4.99,
        "currency": "USD",
        "category_id": "a1000000-0000-0000-0000-000000000001",
        "status": "active",
        "tags": ["dragon", "articulated"],
        "supported_materials": ["PLA", "PETG", "ABS"],
        "compatible_printers": ["FDM"],
        "download_count": 15420,
        "average_rating": 4.8,
        "attr_finish": "smooth"
      }
    }
  ],
  "total_candidates": 42,
  "intent": "semantic_browse",
  "answer": null
}
```

Notes:
- `metadata` is the Qdrant payload for candidates found by vector search. Candidates found only by graph expansion carry just `{"average_rating": ..., "download_count": ...}`.
- `intent` is one of `semantic_browse`, `compatibility_lookup`, `relation_lookup`, `recommendation`, `chatbot_qa`.
- `total_candidates` is the size of the merged vector + graph candidate set before reranking/truncation.
- All score values are floats; the exact scale depends on the stage (see [section 7](#7-retrieval-and-generation-pipelines)).

#### Search filters

`filters` is translated by `app/retrieval/metadata_filters.py::build_qdrant_filters` into Qdrant `must` conditions. Unknown keys are silently ignored. All matches are exact and case-sensitive.

| Filter key | Qdrant payload field | Semantics |
|------------|----------------------|-----------|
| `category_id` | `category_id` | exact match |
| `status` | `status` | exact match |
| `materials` (list) | `supported_materials` | match any |
| `printers` (list) | `compatible_printers` | match any |
| `tags` (list) | `tags` | match any |
| `price_min` / `price_max` | `price` | `gte` / `lte` (cast to float) |
| `min_rating` | `average_rating` | `gte` |
| `attr_<key>` | `attr_<key>` | scalar: exact match; list: match any |

At the Qdrant layer (`VectorStore.hybrid_search`) a filter value that is a list becomes `MatchAny`, a dict with `gte`/`lte` becomes a `Range`, anything else becomes `MatchValue`. No default `status` filter is applied.

### `POST /api/recommend`

Request (`RecommendRequest`): `product_id` (string, min length 1, required), `limit` (int 1-50, default `10`).

```json
{ "product_id": "550e8400-e29b-41d4-a716-446655440001", "limit": 5 }
```

Response: `SearchResponse` (same shape as above) with `intent` fixed to `"recommendation"`. An unknown `product_id` (not found in Qdrant) returns `200` with `results: []` and `total_candidates: 0`.

### `POST /api/chat`

Request (`ChatRequest`): `query` (string, 1-1000 chars, required), `filters` (object, same as search, default `{}`). There is no `limit`; retrieval is run with `limit=10`.

```json
{
  "query": "Which printers are compatible with the articulated dragon?",
  "filters": { "materials": ["PLA"] }
}
```

Response (`ChatResponse`):

```json
{
  "answer": "The Articulated Dragon is listed as compatible with FDM printers and supports PLA, PETG and ABS...",
  "sources": [
    {
      "product_id": "550e8400-e29b-41d4-a716-446655440001",
      "name": "Articulated Dragon",
      "slug": "articulated-dragon",
      "score": 0.8731,
      "vector_score": 0.7412,
      "graph_score": 0.0,
      "rerank_score": 0.9650,
      "metadata": { "...": "Qdrant payload" }
    }
  ],
  "intent": "chatbot_qa"
}
```

`answer` falls back to `"I couldn't generate an answer."` if empty; when retrieval yields no results the generator returns `"I couldn't find any products matching your query."`.

### `GET /health`

Probes Qdrant (`get_collections`), Neo4j (`RETURN 1`) and Ollama (`GET {OLLAMA_BASE_URL}/api/tags`, 5 s timeout). Always returns HTTP `200`; the body carries the status. Excluded from tracing. Note the compose file defines no container healthcheck against it.

```json
{
  "status": "healthy",
  "qdrant": "healthy",
  "neo4j": "healthy",
  "ollama": "healthy"
}
```

`status` is `"healthy"` only if all three are healthy, otherwise `"degraded"`; a failing component is reported as `"unhealthy: <error>"`.

### Gateway exposure

`ApiGateway/ApiGateway/appsettings.json` (YARP) defines:

| Route id | Match | Transform | Cluster destination |
|----------|-------|-----------|---------------------|
| `graphrag-search` | `/graphrag/{**catch-all}` | `PathRemovePrefix: /graphrag` | `http://graphrag-api:8100` |

So clients call, e.g. `POST http://localhost:5133/graphrag/api/search` (gateway host port is `GATEWAY_PORT`, default `5133`) and `GET /graphrag/health`. The route has no authorization policy attached, and the Storefront calls it without a token. Every endpoint, including `/docs`, is reachable through the prefix. The gateway CORS policy allows `http://localhost:3000` and `http://127.0.0.1:3000`.

---

## 5. Messaging (RabbitMQ)

Implemented in `app/workers/consumer.py` (`EventConsumer`). The service **only consumes**; it publishes no events.

| Property | Value |
|----------|-------|
| Exchange | `catalog-exchange` (`RABBITMQ_EXCHANGE`), type `topic`, durable (declared by the consumer; the Catalog publisher declares it identically) |
| Queue | `graphrag-ingestion` (`RABBITMQ_QUEUE`), durable, no dead-letter arguments |
| Prefetch | `10` |
| Connection | `aio_pika.connect_robust(RABBITMQ_URL)` (automatic reconnect) |
| Bindings | One binding per routing key below |

### Routing keys, handlers and effects

| Routing key | Event | Handler | Qdrant | Neo4j |
|-------------|-------|---------|--------|-------|
| `product.created` | `ProductCreated` | `ProductCreatedHandler` | upsert point (re-embed) | upsert product + sync materials/printers + attributes |
| `product.updated` | `ProductUpdated` | `ProductUpdatedHandler` | upsert point (re-embed) | upsert product + sync materials/printers |
| `product.deleted` | `ProductDeleted` | `ProductDeletedHandler` | delete point | `DETACH DELETE` product |
| `product.relation.added` | `ProductRelationAdded` | `RelationAddedHandler` | - | create `RELATED_TO` |
| `product.relation.deleted` | `ProductRelationDeleted` | `RelationDeletedHandler` | - | delete `RELATED_TO` |
| `product.attribute.added` | `ProductAttributeAdded` | `AttributeAddedHandler` | `set_payload` `attr_<Key>` (facet attributes only) | `HAS_ATTRIBUTE` to `Attribute` node |
| `product.attribute.deleted` | `ProductAttributeDeleted` | `AttributeDeletedHandler` | `delete_payload` `attr_<Key>` | delete `HAS_ATTRIBUTE` for the key |

Messages are JSON (UTF-8). Field names are **PascalCase** (the Catalog serializes its contract records with `System.Text.Json` defaults). Payloads below reflect what the Python handlers read; extra fields (e.g. `CreatedAt`, `UpdatedAt`) are ignored. They were derived from `app/ingestion/transformers.py`, the handlers and `tests/conftest.py`, not cross-checked against the Catalog `Contracts` project.

### `product.created`

Required: `ProductId`, `Name`, `Slug`, `ShortDescription`, `LongDescription`, `Price`, `Currency`, `CategoryId`, `Status`. Optional (defaults): `Tags` (`[]`), `SupportedMaterials` (`[]`), `CompatiblePrinters` (`[]`), `DownloadCount` (`0`), `AverageRating` (`0.0`), `Attributes` (`[]`).

```json
{
  "ProductId": "550e8400-e29b-41d4-a716-446655440001",
  "Name": "Articulated Dragon",
  "Slug": "articulated-dragon",
  "ShortDescription": "A fully articulated dragon with snap-fit joints",
  "LongDescription": "Beautiful articulated dragon model with 20+ joints.",
  "Price": 4.99,
  "Currency": "USD",
  "CategoryId": "a1000000-0000-0000-0000-000000000001",
  "Status": "active",
  "Tags": ["dragon", "articulated", "print-in-place"],
  "SupportedMaterials": ["PLA", "PETG"],
  "CompatiblePrinters": ["FDM"],
  "DownloadCount": 15420,
  "AverageRating": 4.8,
  "Attributes": [
    { "Key": "finish", "Value": "smooth" },
    { "Key": "eco-friendly", "Value": "yes" }
  ],
  "CreatedAt": "2024-01-15T10:30:00Z"
}
```

### `product.updated`

Same as `product.created` except **no** `CategoryId` and **no** `Attributes` are read.

```json
{
  "ProductId": "550e8400-e29b-41d4-a716-446655440001",
  "Name": "Articulated Dragon V2",
  "Slug": "articulated-dragon-v2",
  "ShortDescription": "Updated articulated dragon",
  "LongDescription": "Updated description.",
  "Price": 5.99,
  "Currency": "USD",
  "Status": "active",
  "Tags": ["dragon", "v2"],
  "SupportedMaterials": ["PLA", "PETG", "ABS"],
  "CompatiblePrinters": ["FDM"],
  "DownloadCount": 16000,
  "AverageRating": 4.9,
  "UpdatedAt": "2024-06-01T12:00:00Z"
}
```

See [known gaps](#12-known-gaps-and-inconsistencies): this event currently produces a degraded Qdrant point and a stray Neo4j category.

### `product.deleted`

Only `ProductId` is read.

```json
{ "ProductId": "550e8400-e29b-41d4-a716-446655440001" }
```

### `product.relation.added` / `product.relation.deleted`

All four fields are required for both events (`Type` is only used on add).

```json
{
  "ProductId": "550e8400-e29b-41d4-a716-446655440001",
  "RelatedProductId": "550e8400-e29b-41d4-a716-446655440002",
  "Type": "similar",
  "RelationId": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

### `product.attribute.added`

```json
{ "ProductId": "550e8400-e29b-41d4-a716-446655440001", "Key": "finish", "Value": "matte" }
```

### `product.attribute.deleted`

```json
{ "ProductId": "550e8400-e29b-41d4-a716-446655440001", "Key": "finish" }
```

### Consumer behavior: ack, retry, dead-letter

- Each message is handled inside `async with message.process()`. Because every exception inside is caught and logged, the context manager never sees a failure, so **every message is acknowledged**, including malformed JSON, unknown routing keys, missing required fields (`KeyError`) and backend failures (Qdrant/Neo4j/Ollama errors).
- There is **no retry, no nack/requeue and no dead-letter exchange/queue**. A failed event is logged (`Error processing event`, with traceback) and dropped.
- Events are processed concurrently up to the prefetch of 10; there is no per-product ordering guarantee beyond what a single consumer and the broker provide.
- Relation events `MATCH` both products in Neo4j; if either does not exist yet the Cypher is a silent no-op (and the message is still acked), so a relation event that overtakes its product's `product.created` is lost.
- Qdrant point IDs are the raw `ProductId` strings, which must be valid UUIDs (or unsigned ints) for Qdrant to accept them.

---

## 6. Data storage

### 6.1 Canonical document: `ProductDocument`

`app/domain/product_document.py`. Not a DB model and not an event contract; built by `transform_product_created` / `transform_product_updated` and used to feed both stores.

Fields: `id`, `name`, `slug`, `short_description`, `long_description`, `price`, `currency`, `category_id`, `status`, `tags`, `supported_materials`, `compatible_printers`, `attributes` (list of `ProductAttribute(key, value, bucket)`), `download_count`, `average_rating`.

#### Attribute classification

`app/ingestion/attribute_classifier.py::classify_attribute(key)` lower-cases the key and replaces spaces and `-` with `_`, then looks it up (unknown keys default to **facet**):

| Bucket | Keys | Where it goes |
|--------|------|---------------|
| `facet` | `material`, `printer_type`, `size`, `color`, `finish`, `compatibility`, `weight`, `dimensions`, `layer_height`, `nozzle_size`, `infill`, `print_speed`, `bed_temperature`, `nozzle_temperature`, `resolution`, `filament_diameter`, **and any unknown key** | Embedding text ("Specifications"), Qdrant payload `attr_<key>`, Neo4j `Attribute` node |
| `semantic` | `eco_friendly`, `lightweight`, `modular`, `customizable`, `desk_friendly`, `beginner_friendly`, `high_detail`, `snap_fit`, `articulated`, `multipart`, `functional`, `decorative`, `miniature`, `large_format`, `watertight` | Embedding text ("Characteristics"), Neo4j `Attribute` node only |
| `operational` | `download_count`, `average_rating`, `stock`, `status`, `view_count`, `favorite_count`, `print_success_rate` | Neo4j `Attribute` node only (not embedded, not in Qdrant payload) |

The product-level `download_count` and `average_rating` used for boosting come from the event's `DownloadCount` / `AverageRating` fields, not from attributes.

#### Embedding text

`ProductDocument.to_embedding_text()` joins these lines with `\n`:

```
<name>
<short_description>
<long_description>
Tags: <comma-joined tags>                          # if any
Materials: <comma-joined supported_materials>      # if any
Compatible printers: <comma-joined printers>       # if any
Characteristics: <key: value, ...>                 # semantic attributes, if any
Specifications: <key: value, ...>                  # facet attributes, if any
```

Excluded: IDs, price, status, counters, ratings and operational attributes. The text is embedded as-is (no truncation helper is used).

### 6.2 Qdrant

Created at startup by `app/vector/qdrant_client.py::ensure_collection()` (both API and worker call it) only if it does not already exist; existing collections are never migrated.

| Item | Value |
|------|-------|
| Collection | `products` (`QDRANT_COLLECTION`) |
| Dense vector | name `dense`, size `1024` (BGE-M3), distance `Cosine` |
| Sparse vector | name `sparse`, `modifier=IDF` |
| Point ID | `ProductDocument.id` (the Catalog product GUID string) |
| Connection | `QdrantClient(host=QDRANT_HOST, port=QDRANT_PORT)` (REST, port `6333`) |

**Payload** (`ProductDocument.to_qdrant_payload()`):

| Field | Type | Notes |
|-------|------|-------|
| `product_id` | string | |
| `name`, `slug`, `currency`, `status`, `category_id` | string | |
| `price` | float | |
| `tags`, `supported_materials`, `compatible_printers` | string[] | |
| `download_count` | int | |
| `average_rating` | float | |
| `attr_<key>` | string | One field per **facet** attribute; `<key>` is the raw attribute key (not normalized) |

**Payload indexes** (created with the collection):

| Type | Fields |
|------|--------|
| `KEYWORD` | `category_id`, `status`, `tags`, `supported_materials`, `compatible_printers`, `currency` |
| `FLOAT` | `price`, `average_rating`, `download_count` |

`attr_*` fields are not indexed (filters on them still work, unindexed).

**Sparse vectors in practice:** `VectorStore.upsert_product` only writes a `sparse` vector if the embedding provider returns a non-empty one. The default `OllamaEmbeddingProvider.embed_sparse()` is a placeholder that returns `{}`, so with the default setup only the `dense` vector is stored and queries run dense-only. The native `BGEM3EmbeddingProvider` (FlagEmbedding) does produce sparse weights but is not selectable by configuration (`get_embedding_provider(use_native_bge=False)` is always called with the default).

**Operations:**

| Operation | Method |
|-----------|--------|
| Upsert | `VectorStore.upsert_product` (replaces vector and the whole payload) |
| Delete | `VectorStore.delete_product` (`PointIdsList`) |
| Hybrid search | `VectorStore.hybrid_search`: if a sparse query vector exists, two `Prefetch` branches (`dense`, `sparse`) fused with `Fusion.RRF`; otherwise a dense `query_points` with `using="dense"` |
| Similar | `VectorStore.search_similar`: retrieves the seed's dense vector, then dense query excluding the seed via `HasIdCondition` in `must_not` |
| Attribute add/delete | `set_payload` / `delete_payload` on `attr_<Key>` (facet attributes only on add) |

### 6.3 Neo4j

Driver: `neo4j.AsyncGraphDatabase.driver(NEO4J_URI, auth=(NEO4J_USER, NEO4J_PASSWORD))`. Constraints/indexes are ensured at startup by `ensure_constraints()` (retries up to 5 times on `TransientError`, 0.5 s * attempt back-off):

```cypher
CREATE CONSTRAINT product_id IF NOT EXISTS FOR (p:Product) REQUIRE p.id IS UNIQUE
CREATE CONSTRAINT category_id IF NOT EXISTS FOR (c:Category) REQUIRE c.id IS UNIQUE
CREATE CONSTRAINT attribute_key_value IF NOT EXISTS FOR (a:Attribute) REQUIRE (a.key, a.value) IS UNIQUE
CREATE INDEX material_name IF NOT EXISTS FOR (m:Material) ON (m.name)
CREATE INDEX printer_name IF NOT EXISTS FOR (pr:Printer) ON (pr.name)
```

**Graph model:**

| Node label | Key | Properties |
|------------|-----|------------|
| `Product` | `id` (unique) | `name`, `slug`, `short_description`, `price`, `currency`, `status`, `download_count`, `average_rating`, `tags` (list). `long_description` is **not** stored. |
| `Category` | `id` (unique) | `id` only (no name) |
| `Material` | `name` (indexed) | `name` |
| `Printer` | `name` (indexed) | `name` |
| `Attribute` | `(key, value)` (unique pair) | `key`, `value` (no bucket stored; shared between products with the same pair) |

| Relationship | Pattern | Properties |
|--------------|---------|------------|
| `BELONGS_TO` | `(Product)-[:BELONGS_TO]->(Category)` | |
| `SUPPORTS_MATERIAL` | `(Product)-[:SUPPORTS_MATERIAL]->(Material)` | |
| `COMPATIBLE_WITH` | `(Product)-[:COMPATIBLE_WITH]->(Printer)` | |
| `HAS_ATTRIBUTE` | `(Product)-[:HAS_ATTRIBUTE]->(Attribute)` | |
| `RELATED_TO` | `(Product)-[:RELATED_TO {type, relation_id}]->(Product)` | `type` (e.g. `similar`, `complementary`), `relation_id` |

Write behavior (`app/graph/cypher_queries.py`, `GraphRepository`):

- `UPSERT_PRODUCT`: `MERGE` product by `id`, `SET` all properties, `MERGE` the category and `BELONGS_TO`.
- `SYNC_MATERIALS` / `SYNC_PRINTERS`: delete existing `SUPPORTS_MATERIAL` / `COMPATIBLE_WITH` relationships of the product, then re-create from the list. Only executed when the list is non-empty.
- `ADD_ATTRIBUTE`: `MERGE` the `Attribute` node and `HAS_ATTRIBUTE`. On create, every attribute (all buckets) is written.
- `DELETE_ATTRIBUTE`: deletes `HAS_ATTRIBUTE` relationships for the key (orphan `Attribute` nodes remain).
- `ADD_RELATION` / `DELETE_RELATION`: `MERGE` / delete `RELATED_TO` keyed by `relation_id`. The edge is directed from `ProductId` to `RelatedProductId`.
- `DELETE_PRODUCT`: `DETACH DELETE` the product (orphan Category/Material/Printer/Attribute nodes remain).

---

## 7. Retrieval and generation pipelines

### 7.1 Intents

`SearchIntent` (`app/domain/search_query.py`): `semantic_browse`, `compatibility_lookup`, `relation_lookup`, `recommendation`, `chatbot_qa`.

`IntentClassifier` sends `"Classify this query: <query>"` to the **fast** model with `prompts/intent_classification.txt`; the reply is lower-cased, spaces become `_`, and parsed into `SearchIntent`. Anything unrecognized falls back to `semantic_browse` (logged as a warning). In the search pipeline the intent only controls whether graph expansion runs.

### 7.2 Search pipeline (`SearchPipeline`)

Used directly by `/api/search` and as the first step of `/api/chat`.

```
1. Query understanding   IntentClassifier.classify(raw_query)           (LLM call #1, fast model)
                         QueryRewriter.rewrite(raw_query)               (LLM call #2, fast model; skipped for queries of <= 2 words)
2. Candidate generation  build_qdrant_filters -> VectorStore.hybrid_search(effective_query, limit=HYBRID_SEARCH_LIMIT)
3. Graph expansion       only if intent in {relation_lookup, recommendation, semantic_browse}
4. Merge                 merge_results(vector, graph)
5. Rerank                Reranker.rerank(effective_query, merged, top_k=RERANK_TOP_K)
6. Business signals      apply_business_signals(...), then truncate to the request limit
```

- `effective_query` = rewritten query if non-empty, otherwise the raw query. The rewriter falls back to the original if the output is empty or longer than 5x the input.
- The final result count is the request `limit` (`FINAL_TOP_K` is only a fallback when `limit` is falsy, which the API schema never produces).

### 7.3 Graph expansion (`GraphExpansionStrategy`)

1. Takes the **first 10** vector candidates.
2. `GraphRepository.score_candidates` (`SCORE_CANDIDATES`): for each of those 10, counts distinct other candidates among the 10 it is directly connected to (undirected, via `RELATED_TO | COMPATIBLE_WITH | SUPPORTS_MATERIAL | BELONGS_TO`), normalized by the maximum count to `[0, 1]` (rounded to 4 decimals). The result is written to the candidate's `graph_score` in place.
3. `GraphRepository.expand_products` (`EXPAND_MULTIPLE_PRODUCTS`): finds products not in the seed set that are reachable through an outgoing `RELATED_TO`, or that share a `Category`, `Material` or `Printer` with a seed; `LIMIT RERANK_TOP_K`. Each such new candidate gets a constant `graph_score = 0.5`, `vector_score = 0`, and metadata limited to `average_rating` / `download_count`.

### 7.4 Scoring formulas

| Stage | Formula |
|-------|---------|
| Merge (`merge_results`) | `score = 0.7 * vector_score + 0.3 * graph_score`; deduplicates by `product_id`, sorted descending |
| Rerank (`Reranker.rerank`) | Cross-encoder pair `[query, "name \| Tags: ... \| Materials: ... \| Printers: ..."]` for up to the first 100 candidates; `rerank_score` = model output; `score = 0.35 * score + 0.65 * rerank_score`; sorted, truncated to `RERANK_TOP_K` |
| Business boost (`apply_business_signals`) | `score += (average_rating / 5) * 0.1 + (download_count / max_download_count_in_set) * 0.05`; re-sorted |

`vector_score` is the raw Qdrant score: cosine similarity for dense-only queries, an RRF fusion score when a sparse vector is present.

### 7.5 Recommendation pipeline (`RecommendationPipeline`)

```
Seed product id
  -> VectorStore.search_similar(limit=HYBRID_SEARCH_LIMIT)   dense similarity, seed excluded
  -> GraphExpansionStrategy.expand(limit=RERANK_TOP_K)
  -> merge_results
  -> Reranker.rerank(query="products similar to <name>", top_k=RERANK_TOP_K)
  -> apply_business_signals -> truncate to limit
```

No LLM is involved. Note the `<name>` used in the rerank query is the name of the top similar candidate, not of the seed (see [known gaps](#12-known-gaps-and-inconsistencies)).

### 7.6 Chat pipeline (`ChatPipeline`)

1. Run `SearchPipeline.execute(query, filters, limit=10)`.
2. `AnswerGenerator.generate_answer` builds a context from the top **5** results (name, tags, materials, printers, price + currency, rating `/5`) and calls the **quality** model with:

   ```
   User question: <query>

   Product context:
   1. <name>
      Tags: ...
      Materials: ...
      Printers: ...
      Price: <price> <currency>
      Rating: <rating>/5

   Answer the user's question based on the above context.
   ```
3. The answer is attached to the search response; the API returns the 10 results as `sources`.

### 7.7 Prompts

Loaded from `prompts/` at construction time (`IntentClassifier`, `QueryRewriter`, `AnswerGenerator`); if a file is missing, a shorter built-in default in the corresponding Python class is used.

| File | Used by | Purpose |
|------|---------|---------|
| `prompts/intent_classification.txt` | `IntentClassifier` | Defines the five intents with examples; reply must be the category name only; default `semantic_browse` |
| `prompts/query_rewrite.txt` | `QueryRewriter` | Expand abbreviations (PLA, ABS, PETG, TPU, FDM, SLA, DLP), normalize printer names, add synonyms; output only the rewritten query; includes few-shot examples |
| `prompts/answer_generation.txt` | `AnswerGenerator` | Strict grounding rules: only facts from context, no invented names/specs, say "I don't have enough information to answer that based on the available products" when unsupported, concise (<300 words) |

---

## 8. Configuration

`app/core/settings.py` (`pydantic-settings`, case-insensitive env var names matching the field names; optional `.env` in the working directory; unknown keys ignored). In compose the values below are injected as environment variables.

| Env var | Default | Description |
|---------|---------|-------------|
| `QDRANT_HOST` | `localhost` | Qdrant host |
| `QDRANT_PORT` | `6333` | Qdrant REST port |
| `QDRANT_COLLECTION` | `products` | Collection name |
| `NEO4J_URI` | `bolt://localhost:7687` | Bolt URI |
| `NEO4J_USER` | `neo4j` | Neo4j user |
| `NEO4J_PASSWORD` | dev placeholder in code | Neo4j password; always override |
| `RABBITMQ_URL` | `amqp://guest:guest@localhost:5672/` | AMQP URL (broker default dev credentials) |
| `RABBITMQ_EXCHANGE` | `catalog-exchange` | Topic exchange to bind to |
| `RABBITMQ_QUEUE` | `graphrag-ingestion` | Durable queue |
| `OLLAMA_BASE_URL` | `http://localhost:11434` | Ollama endpoint (embeddings, LLMs, health check) |
| `EMBEDDING_MODEL` | `bge-m3` | Ollama embedding model |
| `RERANKER_MODEL` | `bge-reranker-v2-m3` | Loaded from Hugging Face as `BAAI/<value>` |
| `GENERATION_MODEL` | `gemma3:4b` | Fast LLM |
| `GENERATION_MODEL_LARGE` | `gemma3:12b` | Quality LLM |
| `HYBRID_SEARCH_LIMIT` | `50` | Qdrant candidate limit |
| `GRAPH_EXPANSION_DEPTH` | `2` | Defined but not used by any code |
| `RERANK_TOP_K` | `20` | Graph expansion limit and reranker output size |
| `FINAL_TOP_K` | `10` | Fallback result count when `limit` is not given |
| `LOG_LEVEL` | `INFO` | structlog filtering level |
| `OTEL_ENDPOINT` | `http://localhost:4317` | OTLP gRPC endpoint (Jaeger) |
| `SERVICE_NAME` | `graphrag-service` | OTel `service.name` |
| `OTEL_ENABLED` | `true` | Toggle tracing |

Container-only variable (read by `docker/entrypoint.sh`, not by `Settings`):

| Env var | Default | Description |
|---------|---------|-------------|
| `SERVICE_MODE` | `api` | `api` (uvicorn on `0.0.0.0:8100`), `worker` (`python -m app.workers.consumer`), `both` (worker in background + API in foreground); anything else exits with code 1 |

Compose-level variables in `docker/template.env`: `GRAPHRAG_API_PORT` (`8100`), `GRAPHRAG_EMBEDDING_MODEL`, `GRAPHRAG_RERANKER_MODEL`, `GRAPHRAG_GENERATION_MODEL`, `GRAPHRAG_GENERATION_MODEL_LARGE`, `GRAPHRAG_LOG_LEVEL`, plus the shared `NEO4J_USER`/`NEO4J_PASSWORD`, `RABBITMQ_USER`/`RABBITMQ_PASSWORD`, `QDRANT_PORT`, `OLLAMA_PORT` (`11435`), `OTEL_*`. Credentials are intentionally left blank in the template and must be supplied in `docker/.env`.

---

## 9. Observability

- **Tracing (API process):** `configure_telemetry(app)` runs in the FastAPI lifespan. It exports spans via OTLP gRPC (`OTLPSpanExporter(endpoint=OTEL_ENDPOINT, insecure=True)`, `BatchSpanProcessor`) with resource `service.name = SERVICE_NAME`. Instrumentation: FastAPI (excluding `/health`), httpx (covers Ollama calls), aio-pika, and standard `logging`. In compose the endpoint is `http://jaeger:4317`; Jaeger UI is on `OTEL_UI_PORT` (`16686`).
- **Worker tracing:** `configure_telemetry` is only called from the API lifespan. `app/workers/consumer.py::main` calls only `configure_logging()`, so the worker currently emits no traces even though compose sets `OTEL_*` for it.
- **Logging:** `structlog` with `ConsoleRenderer` (human-readable, not JSON) to stdout; ISO timestamps, level, `LOG_LEVEL` filtering. When tracing is active, `trace_id` and `span_id` are injected into log events.
- **Health:** `GET /health` (see [section 4](#get-health)).

---

## 10. Running locally

### 10.1 Docker Compose (recommended)

Compose entries live in `docker/docker-compose.yml` (project name `ecommerce-infra`):

| Service | Container | Port mapping | Notes |
|---------|-----------|--------------|-------|
| `graphrag-api` | `graphrag-api` | `${GRAPHRAG_API_PORT}:8100` | `SERVICE_MODE=api`; depends on qdrant, neo4j, rabbitmq, ollama, jaeger |
| `graphrag-worker` | `graphrag-worker` | none | `SERVICE_MODE=worker`; same image/build (`context: ../GraphRAG`, `docker/Dockerfile`) |
| `ollama` | `ollama` | `${OLLAMA_PORT}:11434` (`11435` host) | `ollama/ollama:latest`, volume `ollama_data`, **reserves all NVIDIA GPUs** (remove the `deploy` block on machines without a GPU) |
| `qdrant` | `qdrant` | `${QDRANT_PORT}:6333`, `${QDRANT_UI_PORT}:6334` | volume `qdrant_data` |
| `neo4j` | `neo4j` | `${NEO4J_HTTP_PORT}:7474`, `${NEO4J_BOLT_PORT}:7687` | volume `neo4j_data`, `NEO4J_AUTH=${NEO4J_USER}/${NEO4J_PASSWORD}` |
| `rabbitmq` | `rabbitmq` | `${RABBITMQ_PORT}:5672`, `${RABBITMQ_UI_PORT}:15672` | management image |

Both GraphRAG containers use `entrypoint: ["bash", "/app/docker/entrypoint.sh"]`, which overrides the Dockerfile `CMD`. The Dockerfile itself is `python:3.11-slim` + `build-essential`, `pip install -r requirements.txt`, `EXPOSE 8100`.

```bash
cd docker
cp template.env .env      # fill in NEO4J_*, RABBITMQ_*, MINIO_*, STRIPE_* etc.
docker compose up -d --build

# Pull Ollama models once (the container does not auto-pull)
docker exec ollama ollama pull bge-m3
docker exec ollama ollama pull gemma3:4b
docker exec ollama ollama pull gemma3:12b
```

The reranker (`BAAI/bge-reranker-v2-m3`) is **not** pulled through Ollama; it is downloaded from Hugging Face inside the `graphrag-api` container on the first search/recommend/chat request (no persistent cache volume is mounted, so it is re-downloaded when the container is recreated).

### 10.2 Native (API + worker on the host)

Requires Python 3.11+, running Qdrant, Neo4j, RabbitMQ and Ollama (for example via `docker compose up -d qdrant neo4j rabbitmq ollama jaeger`).

```bash
cd GraphRAG
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\Activate.ps1
pip install -r requirements.txt                      # includes sentence-transformers (needed by the reranker)
pip install pytest pytest-asyncio pytest-cov ruff    # or: pip install -e ".[dev]" plus sentence-transformers
```

Create `GraphRAG/.env` (never commit it). The compose Ollama is published on host port `11435`, not `11434`:

```env
QDRANT_HOST=localhost
QDRANT_PORT=6333
NEO4J_URI=bolt://localhost:7687
NEO4J_USER=<your neo4j user>
NEO4J_PASSWORD=<your neo4j password>
RABBITMQ_URL=amqp://<user>:<password>@localhost:5672/
OLLAMA_BASE_URL=http://localhost:11435
OTEL_ENDPOINT=http://localhost:4317
```

```bash
# Terminal 1: API
uvicorn app.api.main:app --reload --port 8100

# Terminal 2: ingestion worker
python -m app.workers.consumer
```

`python -m app.workers` also works (`app/workers/__main__.py`).

### 10.3 Seeding

`scripts/seed.py` does not touch Qdrant/Neo4j directly. It POSTs through the API Gateway to Catalog (`POST /products/`, `/products/{id}/attributes`, `/products/{id}/relations`), creating 10 sample products (6 fixed category GUIDs) and 9 relations; the worker then ingests the resulting events.

```bash
python -m scripts.seed                                   # default gateway http://localhost:5133
python -m scripts.seed --gateway-url http://localhost:5133
```

Wait a few seconds, then query, for example:

```bash
curl -X POST http://localhost:8100/api/search -H "Content-Type: application/json" \
     -d '{"query":"articulated dragon","limit":5}'
curl -X POST http://localhost:5133/graphrag/api/search -H "Content-Type: application/json" \
     -d '{"query":"articulated dragon","limit":5}'    # through the gateway
```

(The repo-level `scripts/seed-dev-data.py` also seeds the whole stack and waits for GraphRAG ingestion.)

### 10.4 Reindexing

`python -m scripts.reindex` rebuilds the **Qdrant** collection from **Neo4j** (it does not touch Neo4j): it ensures the collection exists, reads every `Product` with its category, materials, printers and attributes, rebuilds a `ProductDocument` and calls `VectorStore.upsert_product` (re-embedding via Ollama), logging every 100 products. Because Neo4j does not store `long_description`, reindexed embedding text is built with an empty long description, so vectors differ from those produced by the original events. Missing Neo4j properties fall back to defaults (`currency=USD`, `status=active`, `price=0`).

---

## 11. Testing

```bash
cd GraphRAG
pytest tests/unit -v                    # no external services needed
pytest tests/integration -v             # requires Qdrant, Neo4j, Ollama
pytest --cov=app                        # coverage (pytest-cov)
ruff check .
```

| Suite | Files | Covers |
|-------|-------|--------|
| Unit | `test_attribute_classifier.py`, `test_metadata_filters.py`, `test_product_document.py`, `test_result_merger.py`, `test_scoring.py`, `test_transformers.py` | Classification buckets, filter translation, embedding text/payload building, merge weights and dedup, business scoring, event transformation |
| Integration | `tests/integration/test_api.py` | `/health` shape, request validation (`422` on empty query, `limit > 100`, empty `product_id`), and that valid search/chat/recommend requests return `200` or `500`. Uses `httpx.ASGITransport`, which does not run the app lifespan. |

Shared fixtures in `tests/conftest.py` provide sample `ProductCreated` / `ProductUpdated` payloads. `pytest` is configured with `asyncio_mode = "auto"` and `testpaths = ["tests"]`. Rerankers, LLM calls, handlers, the consumer, repositories and the pipelines have no dedicated tests.

---

## 12. Known gaps and inconsistencies

Observed from the code (not fixed here):

1. **Graph connectivity score is lost in the merge.** `GraphExpansionStrategy.expand` writes connectivity scores onto the vector candidates in place, but `merge_results` rebuilds every vector candidate with `graph_score=0.0` and only copies graph scores from graph-expanded candidates (which never include IDs already in the vector set). Net effect: for vector hits `graph_score` is always `0.0`; only graph-only candidates carry `0.5`.
2. **`product.updated` degrades the indexes.** `transform_product_updated` sets `category_id=""` and `attributes=[]`, then the same upsert path is used. Qdrant: the point payload is replaced (so `category_id` becomes `""` and all `attr_*` fields are dropped) and the vector is re-embedded without "Characteristics"/"Specifications". Neo4j: `MERGE (:Category {id: ""})` and an extra `BELONGS_TO` edge to it are created; old attributes remain.
3. **Materials/printers are not cleared** in Neo4j when an event sends an empty list (the sync Cypher is skipped for empty lists), and `UPSERT_PRODUCT` never removes an existing `BELONGS_TO` edge before adding a new one.
4. **Semantic attributes added later are not re-embedded** (`AttributeAddedHandler` only updates Neo4j and, for facets, the Qdrant payload; noted in a code comment).
5. **No reliability on the consumer:** all messages are acked even on failure; no retry or DLQ; relation events can be lost if they arrive before the products exist.
6. **Sparse/hybrid search is effectively dense-only** with the default Ollama embedding provider (`embed_sparse` returns `{}`); the BGE-M3/FlagEmbedding provider cannot be enabled via config.
7. **Recommendation rerank query** uses the name of the first similar candidate (`vector_candidates[0]`) rather than the seed product's name.
8. **Worker is not traced** (telemetry configured only in the API lifespan).
9. **Blocking Qdrant client** (synchronous `QdrantClient`) is called from async code, blocking the event loop during Qdrant I/O.
10. **Error handling:** routes have no try/except; any dependency failure (including the two LLM calls in search) returns `500`. `/health` never returns a non-200 status.
11. **Unused/dead items:** `GRAPH_EXPANSION_DEPTH`, `app/utils/*`, `app/domain/events.py::ProductEvent`, `langchain*`/`tenacity` dependencies, `BGEM3EmbeddingProvider` (not selectable). `pyproject.toml` omits `sentence-transformers` (and implicitly `torch`) which `requirements.txt` includes, so `pip install -e .` alone is insufficient for the reranker.
12. **Reranker first-request latency and size:** the cross-encoder loads lazily on first request (HF download, CPU by default in the container).
13. **Stale statements in the root `README.md` GraphRAG section:** it says all models run via Ollama and tells you to `ollama pull bge-reranker-v2-m3` (the reranker is loaded from Hugging Face via sentence-transformers); it mentions `normalize=True` for the reranker (not present in code); it advertises dense + sparse vectors and RRF without noting sparse vectors are empty by default; it says operational attributes are stored as "payload / graph property" (they are only `Attribute` nodes in Neo4j; boosting uses the event's `DownloadCount`/`AverageRating`); its quick start uses `ollama pull` on the host and `pip install -e ".[dev]"`, while compose's Ollama is published on host port `11435` and `pyproject.toml` lacks `sentence-transformers`; it presents graph proximity as part of the merge score without noting the bug in item 1.

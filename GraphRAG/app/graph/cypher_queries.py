# --- Product CRUD ---

UPSERT_PRODUCT = """
MERGE (p:Product {id: $id})
SET p.name = $name,
    p.slug = $slug,
    p.short_description = $short_description,
    p.price = $price,
    p.currency = $currency,
    p.status = $status,
    p.download_count = $download_count,
    p.average_rating = $average_rating,
    p.tags = $tags
WITH p
MERGE (c:Category {id: $category_id})
MERGE (p)-[:BELONGS_TO]->(c)
RETURN p
"""

DELETE_PRODUCT = """
MATCH (p:Product {id: $id})
DETACH DELETE p
"""

# --- Materials ---

SYNC_MATERIALS = """
MATCH (p:Product {id: $product_id})
OPTIONAL MATCH (p)-[r:SUPPORTS_MATERIAL]->(m:Material)
DELETE r
WITH p
UNWIND $materials AS material_name
MERGE (m:Material {name: material_name})
MERGE (p)-[:SUPPORTS_MATERIAL]->(m)
"""

# --- Printers ---

SYNC_PRINTERS = """
MATCH (p:Product {id: $product_id})
OPTIONAL MATCH (p)-[r:COMPATIBLE_WITH]->(pr:Printer)
DELETE r
WITH p
UNWIND $printers AS printer_name
MERGE (pr:Printer {name: printer_name})
MERGE (p)-[:COMPATIBLE_WITH]->(pr)
"""

# --- Attributes ---

ADD_ATTRIBUTE = """
MATCH (p:Product {id: $product_id})
MERGE (a:Attribute {key: $key, value: $value})
MERGE (p)-[:HAS_ATTRIBUTE]->(a)
"""

DELETE_ATTRIBUTE = """
MATCH (p:Product {id: $product_id})-[r:HAS_ATTRIBUTE]->(a:Attribute {key: $key})
DELETE r
"""

# --- Relations ---

ADD_RELATION = """
MATCH (p:Product {id: $product_id})
MATCH (rp:Product {id: $related_product_id})
MERGE (p)-[r:RELATED_TO {type: $relation_type, relation_id: $relation_id}]->(rp)
"""

DELETE_RELATION = """
MATCH (p:Product {id: $product_id})-[r:RELATED_TO {relation_id: $relation_id}]->(rp:Product {id: $related_product_id})
DELETE r
"""

# --- Graph Expansion Queries ---

EXPAND_PRODUCT = """
MATCH (p:Product {id: $product_id})
OPTIONAL MATCH (p)-[:RELATED_TO]->(related:Product)
OPTIONAL MATCH (p)-[:BELONGS_TO]->(c:Category)<-[:BELONGS_TO]-(same_cat:Product)
WHERE same_cat.id <> p.id
OPTIONAL MATCH (p)-[:SUPPORTS_MATERIAL]->(m:Material)<-[:SUPPORTS_MATERIAL]-(same_mat:Product)
WHERE same_mat.id <> p.id
OPTIONAL MATCH (p)-[:COMPATIBLE_WITH]->(pr:Printer)<-[:COMPATIBLE_WITH]-(same_printer:Product)
WHERE same_printer.id <> p.id
RETURN
    collect(DISTINCT related.id) AS related_ids,
    collect(DISTINCT same_cat.id)[..10] AS same_category_ids,
    collect(DISTINCT same_mat.id)[..10] AS same_material_ids,
    collect(DISTINCT same_printer.id)[..10] AS same_printer_ids
"""

EXPAND_MULTIPLE_PRODUCTS = """
UNWIND $product_ids AS pid
MATCH (p:Product {id: pid})
OPTIONAL MATCH (p)-[:RELATED_TO]->(related:Product)
WHERE NOT related.id IN $product_ids
OPTIONAL MATCH (p)-[:BELONGS_TO]->(c:Category)<-[:BELONGS_TO]-(neighbor:Product)
WHERE NOT neighbor.id IN $product_ids AND neighbor.id <> p.id
OPTIONAL MATCH (p)-[:SUPPORTS_MATERIAL]->(m:Material)<-[:SUPPORTS_MATERIAL]-(mat_neighbor:Product)
WHERE NOT mat_neighbor.id IN $product_ids AND mat_neighbor.id <> p.id
OPTIONAL MATCH (p)-[:COMPATIBLE_WITH]->(pr:Printer)<-[:COMPATIBLE_WITH]-(printer_neighbor:Product)
WHERE NOT printer_neighbor.id IN $product_ids AND printer_neighbor.id <> p.id
WITH
    collect(DISTINCT related) AS all_related,
    collect(DISTINCT neighbor) AS all_neighbors,
    collect(DISTINCT mat_neighbor) AS all_mat_neighbors,
    collect(DISTINCT printer_neighbor) AS all_printer_neighbors
UNWIND (all_related + all_neighbors + all_mat_neighbors + all_printer_neighbors) AS candidate
RETURN DISTINCT candidate.id AS product_id,
       candidate.name AS name,
       candidate.slug AS slug,
       candidate.average_rating AS average_rating,
       candidate.download_count AS download_count
LIMIT $limit
"""

# --- Intra-result connectivity scoring ---

SCORE_CANDIDATES = """
UNWIND $product_ids AS pid
MATCH (p:Product {id: pid})
OPTIONAL MATCH (p)-[:RELATED_TO|COMPATIBLE_WITH|SUPPORTS_MATERIAL|BELONGS_TO]-(neighbor:Product)
WHERE neighbor.id IN $product_ids AND neighbor.id <> pid
RETURN pid AS product_id, count(DISTINCT neighbor) AS connection_count
"""

GET_PRODUCT_CONTEXT = """
MATCH (p:Product {id: $product_id})
OPTIONAL MATCH (p)-[:BELONGS_TO]->(c:Category)
OPTIONAL MATCH (p)-[:SUPPORTS_MATERIAL]->(m:Material)
OPTIONAL MATCH (p)-[:COMPATIBLE_WITH]->(pr:Printer)
OPTIONAL MATCH (p)-[:HAS_ATTRIBUTE]->(a:Attribute)
OPTIONAL MATCH (p)-[:RELATED_TO]->(rel:Product)
RETURN p,
       c.id AS category_id,
       collect(DISTINCT m.name) AS materials,
       collect(DISTINCT pr.name) AS printers,
       collect(DISTINCT {key: a.key, value: a.value}) AS attributes,
       collect(DISTINCT {id: rel.id, name: rel.name}) AS relations
"""
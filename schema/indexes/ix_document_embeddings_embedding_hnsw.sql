CREATE INDEX ix_document_embeddings_embedding_hnsw
    ON public.document_embeddings
    USING hnsw (embedding public.vector_cosine_ops);

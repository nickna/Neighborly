FROM ollama/ollama:0.1.44

ARG MODEL_NAME=phi3
ARG EMBEDDING_MODEL_NAME=all-minilm
RUN nohup bash -c "ollama serve &" && sleep 5 && ollama pull "$MODEL_NAME" && ollama pull "$EMBEDDING_MODEL_NAME"

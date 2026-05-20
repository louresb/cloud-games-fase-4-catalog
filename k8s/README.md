# Kubernetes

Este documento descreve as configuraÃ§Ãµes e detalhes da orquestraÃ§Ã£o de containers utilizando Kubernetes para o serviÃ§o de `catalog` da aplicaÃ§Ã£o de microsserviÃ§os desenvolvida na Fase 2 do Tech Challenge da FIAP.

## Ãndice
- ConfiguraÃ§Ãµes
  - [Namespace](#namespace)
  - [External Names](#external-names)
  - [Catalog](#catalog)
- [Comandos Ãšteis](#comandos-uteis)

> AtenÃ§Ã£o! 
> 
> Os manifestos de secrets `k8s\*-secret.yaml` nÃ£o estÃ£o incluÃ­dos no repositÃ³rio (e Ã© ignorado pelo `.gitignore`) por conter informaÃ§Ãµes sensÃ­veis, como senhas.
> 
> VocÃª pode copiar o seu respectivo arquivo de exemplo `k8s\templates\*-secret.yaml` e ajustar os valores.

<a id="namespace"></a>
### Namespace

> Isola os recursos de apps em um namespace dedicado.

| Arquivo | `k8s\fcg-apps-namespace.yaml` |
|---|---|
| apiVersion | `v1` |
| kind | `Namespace` |
| metadata.name | `fcg-apps` |
| metadata.labels | `environment: development` |

<a id="external-names"></a>
### External Names

> Mapeia serviÃ§os externos (SQL Server, RabbitMQ, etc) para dentro do cluster Kubernetes usando `ExternalName`.

| Arquivo | `k8s\externalnames-service.yaml` |
|---|---|
| apiVersion | `v1` |
| kind | `Service` |
| metadata.name | `sqlserver-service` |
| metadata.namespace | `fcg-apps` |
| spec.type | `ExternalName` |
| spec.externalName | `sqlserver-service.fcg-infra.svc.cluster.local` |
|---|---|
| apiVersion | `v1` |
| kind | `Service` |
| metadata.name | `rabbitmq-service` |
| metadata.namespace | `fcg-apps` |
| spec.type | `ExternalName` |
| spec.externalName | `rabbitmq-service.fcg-infra.svc.cluster.local` |
|---|---|
| apiVersion | `v1` |
| kind | `Service` |
| metadata.name | `loki-service` |
| metadata.namespace | `fcg-apps` |
| spec.type | `ExternalName` |
| spec.externalName | `loki-service.fcg-infra.svc.cluster.local` |

<a id="catalog"></a>
### Catalog

A seguir estÃ£o as descriÃ§Ãµes dos manifestos relacionados ao serviÃ§o `catalog` (configuraÃ§Ãµes, secrets, service e deployment).

#### Secret

| Arquivo | `k8s\catalog-secret.yaml` |
|---|---|
| apiVersion | `v1` |
| kind | `Secret` |
| metadata.name | `catalog-secret` |
| metadata.namespace | `fcg-apps` |
| metadata.labels | `app: catalog-api` |
| type / data | `stringData` com placeholders para configuraÃ§Ãµes sensÃ­veis (credenciais RabbitMQ). |
| Exemplos de chaves | `ConnectionStrings__DefaultConnection`, `RabbitMq__UserName`, `RabbitMq__Password`, `Jwt__Secret` |
| ObservaÃ§Ã£o | NÃ£o commitar segredos reais no repositÃ³rio; copie o template e substitua valores antes de aplicar. |

#### ConfigMap

| Arquivo | `k8s\catalog-configmap.yaml` |
|---|---|
| apiVersion | `v1` |
| kind | `ConfigMap` |
| metadata.name | `catalog-config` |
| metadata.namespace | `fcg-apps` |
| metadata.labels | `app: catalog-api` |
| data (principais chaves) | `ASPNETCORE_ENVIRONMENT: Production`, `Queues__Users__Commands: users.commands`, `Queues__Users__Events: users.events`, `Queues__Catalog__Commands: catalog.commands`, `Queues__Catalog__Events: catalog.events`, `Queues__Payments__Commands: payments.commands`, `Queues__Payments__Events: payments.events`, `Queues__Notifications__Commands: notifications.commands`, `Queues__Notifications__Events: notifications.events`, `RabbitMq__HostName: rabbitmq-service.fcg-infra.svc.cluster.local`, `Loki__Url: http://loki-service.fcg-infra.svc.cluster.local:3100`, `Jwt__Issuer: cloud-games`, `Jwt__Audience: cloud-games-audience`, `Jwt__ExpiryMinutes: 60` |

#### Service

| Arquivo | `k8s\catalog-service.yaml` |
|---|---|
| apiVersion | `v1` |
| kind | `Service` |
| metadata.name | `catalog-service` |
| metadata.namespace | `fcg-apps` |
| metadata.labels | `app: catalog-api` |
| spec.type | `NodePort` |
| spec.selector | `app: catalog-api` |
| spec.ports[0].port | `80` |
| spec.ports[0].targetPort | `8080` |
| spec.ports[0].nodePort | `30081` |

#### Deployment

| Arquivo | `k8s\catalog-deployment.yaml` |
|---|---|
| apiVersion | `apps/v1` |
| kind | `Deployment` |
| metadata.name | `catalog-deployment` |
| metadata.namespace | `fcg-apps` |
| metadata.labels | `app: catalog-api` |
| spec.replicas | `1` |
| spec.selector.matchLabels | `app: catalog-api` |
| template.spec.containers[0].name | `catalog-api` |
| template.spec.containers[0].image | `cloud-games-catalog-svc:latest` |
| template.spec.containers[0].imagePullPolicy | `IfNotPresent` |
| template.spec.containers[0].ports | containerPort `8080` |
| template.spec.containers[0].envFrom | - `configMapRef.name: catalog-config` and `secretRef.name: catalog-secret` (carrega variÃ¡veis de ambiente do ConfigMap e do Secret) |
| template.spec.containers[0].livenessProbe | httpGet `/health/live` porta `8080`, `initialDelaySeconds: 10`, `periodSeconds: 10` |
| template.spec.containers[0].readinessProbe | httpGet `/health/ready` porta `8080`, `initialDelaySeconds: 5`, `periodSeconds: 10` |

<a id="comandos-uteis"></a>
### Comandos Ãšteis

- Build da imagem Docker (executar na raiz do repositÃ³rio):
  ```bash
  docker build -t cloud-games-catalog-svc:latest .
  ```

- Aplicar todos os manifestos (na ordem correta):
  ```bash
  kubectl apply -f k8s/fcg-apps-namespace.yaml
  kubectl apply -f k8s/externalnames-service.yaml
  kubectl apply -f k8s/catalog-secret.yaml
  kubectl apply -f k8s/catalog-configmap.yaml
  kubectl apply -f k8s/catalog-service.yaml
  kubectl apply -f k8s/catalog-deployment.yaml
  ```

- Verificar serviÃ§os:
  ```bash
  kubectl get services -n fcg-apps
  ```
  
- Verificar pods:
  ```bash
  kubectl get pods -n fcg-apps
  ```
  
- Verificar detalhes de um pod:
  ```bash
  kubectl describe pod <nome-do-pod> -n fcg-apps
  ```
  
- Verificar logs de um pod:
  ```bash
  ## Logs de um pod especÃ­fico:
  kubectl logs <nome-do-pod> -n fcg-apps
  ## Logs de um deployment (pega o pod automaticamente):
  kubectl logs deployment/catalog-deployment -n fcg-apps
  ## Logs em tempo real:
  kubectl logs -f <nome-do-pod> -n fcg-apps
  ## Ãšltimas 100 linhas:
  kubectl logs <nome-do-pod> -n fcg-apps --tail=100
  ```
  
- Acessar um pod via shell:
  ```bash
  ## Acessar um pod especÃ­fico:
  kubectl exec -it <nome-do-pod> -n fcg-apps -- /bin/bash
  ## Acessar pelo deployment (pega o pod automaticamente):
  kubectl exec -it deployment/catalog-deployment -n fcg-apps -- /bin/bash
  ```

- Resetar o deployment (forÃ§a reinÃ­cio):
  ```bash
  kubectl rollout restart deployment/catalog-deployment -n fcg-apps
  ```

- Remover namespace (remove todos os recursos dentro do namespace):
  ```bash
  kubectl delete namespace fcg-apps
  ```


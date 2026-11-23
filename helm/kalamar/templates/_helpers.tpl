{{/* Chart adı (nameOverride ile ezilebilir). */}}
{{- define "kalamar.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Kaynak adlarının ön eki. Release adı chart adını içeriyorsa tekrarlanmaz:
release "kalamar-dev" -> "kalamar-dev-user", release "demo" -> "demo-kalamar-user".
*/}}
{{- define "kalamar.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{- define "kalamar.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/* Bir bileşenin kaynak adı. Kullanım: include "kalamar.componentName" (dict "root" $ "component" "user") */}}
{{- define "kalamar.componentName" -}}
{{- printf "%s-%s" (include "kalamar.fullname" .root) .component | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/* Standart label'lar. Kullanım: include "kalamar.labels" (dict "root" $ "component" "user") */}}
{{- define "kalamar.labels" -}}
helm.sh/chart: {{ include "kalamar.chart" .root }}
{{ include "kalamar.selectorLabels" . }}
app.kubernetes.io/version: {{ .root.Chart.AppVersion | quote }}
app.kubernetes.io/part-of: kalamar
app.kubernetes.io/managed-by: {{ .root.Release.Service }}
{{- end }}

{{/* Selector label'ları. Deployment oluşturulduktan sonra değiştirilemezler. */}}
{{- define "kalamar.selectorLabels" -}}
app.kubernetes.io/name: {{ include "kalamar.name" .root }}
app.kubernetes.io/instance: {{ .root.Release.Name }}
app.kubernetes.io/component: {{ .component }}
{{- end }}

{{/*
Uygulama image'ı: <registry>/<repositoryPrefix><component>:<tag>.
apps.<x>.image.repository / apps.<x>.image.tag ile uygulama bazında ezilebilir.
Kullanım: include "kalamar.image" (dict "root" $ "component" "user" "app" $app)
*/}}
{{- define "kalamar.image" -}}
{{- $global := .root.Values.image }}
{{- $override := .app.image | default dict }}
{{- $repository := $override.repository | default (printf "%s/%s%s" $global.registry $global.repositoryPrefix .component) }}
{{- $tag := $override.tag | default $global.tag | default .root.Chart.AppVersion }}
{{- printf "%s:%s" $repository (toString $tag) }}
{{- end }}

{{/* SA şifresini tutan Secret'ın adı. */}}
{{- define "kalamar.sqlserver.secretName" -}}
{{- if .Values.sqlserver.existingSecret }}
{{- .Values.sqlserver.existingSecret }}
{{- else }}
{{- include "kalamar.componentName" (dict "root" . "component" "sqlserver") }}
{{- end }}
{{- end }}

{{/* SA şifresinin Secret içindeki anahtarı. Chart'ın ürettiği Secret'ta her zaman MSSQL_SA_PASSWORD'dur. */}}
{{- define "kalamar.sqlserver.secretKey" -}}
{{- if .Values.sqlserver.existingSecret }}
{{- .Values.sqlserver.existingSecretKey | default "MSSQL_SA_PASSWORD" }}
{{- else }}
{{- "MSSQL_SA_PASSWORD" }}
{{- end }}
{{- end }}

{{/* Şifre kaynağı verilmemişse anlaşılır bir hatayla durur. */}}
{{- define "kalamar.sqlserver.validate" -}}
{{- if and (not .Values.sqlserver.existingSecret) (not .Values.sqlserver.sealedSaPassword) }}
{{- fail "\n\nSQL Server SA sifresi tanimli degil. Su degerlerden birini verin:\n  sqlserver.existingSecret  = hazir bir Secret'in adi (anahtar: MSSQL_SA_PASSWORD)\n  sqlserver.sealedSaPassword = 'kubeseal --raw' ciktisi\nAyrinti: helm/kalamar/values.yaml" }}
{{- end }}
{{- end }}

{{/* MSSQL_SA_PASSWORD env girdisi (SQL Server ve Persistence ortak kullanır). */}}
{{- define "kalamar.sqlserver.passwordEnv" -}}
- name: MSSQL_SA_PASSWORD
  valueFrom:
    secretKeyRef:
      name: {{ include "kalamar.sqlserver.secretName" . }}
      key: {{ include "kalamar.sqlserver.secretKey" . }}
{{- end }}

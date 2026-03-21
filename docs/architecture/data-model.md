# Proposta Inicial de Modelagem PostgreSQL

## Premissas

- um banco por servico, todos em PostgreSQL
- um container PostgreSQL local pode hospedar multiplos databases
- tabelas com `SchoolId` sempre que o dado for tenant-scoped
- referencias entre servicos sao armazenadas como IDs externos, sem FK cross-database

## Identity

### `user_accounts`

- `id`
- `school_id` nullable para `SystemAdmin`
- `email`
- `password_hash`
- `role`
- `is_active`
- `must_change_password`
- `last_login_at_utc`
- `created_at_utc`

### `refresh_sessions`

- `id`
- `user_id`
- `token_hash`
- `device_name`
- `expires_at_utc`
- `revoked_at_utc`
- `created_at_utc`

## Schools

### `schools`

- `id`
- `legal_name`
- `display_name`
- `slug`
- `status`
- `timezone`
- `currency_code`
- `created_at_utc`

### `school_settings`

- `id`
- `school_id`
- `booking_lead_time_minutes`
- `cancellation_window_hours`
- `theme_primary`
- `theme_accent`
- `created_at_utc`

### `user_profiles`

- `id`
- `school_id`
- `identity_user_id`
- `full_name`
- `phone`
- `avatar_url`
- `is_active`
- `created_at_utc`

## Academics

### `students`

- `id`
- `school_id`
- `full_name`
- `email`
- `phone`
- `birth_date`
- `medical_notes`
- `emergency_contact_name`
- `emergency_contact_phone`
- `first_stand_up_at_utc`
- `is_active`
- `created_at_utc`

### `instructors`

- `id`
- `school_id`
- `identity_user_id` nullable
- `full_name`
- `email`
- `phone`
- `specialties`
- `is_active`
- `created_at_utc`

### `courses`

- `id`
- `school_id`
- `name`
- `level`
- `total_lessons`
- `price`
- `is_active`
- `created_at_utc`

### `enrollments`

- `id`
- `school_id`
- `student_id`
- `course_id`
- `status`
- `included_lessons_snapshot`
- `used_lessons`
- `course_price_snapshot`
- `started_at_utc`
- `ended_at_utc`
- `created_at_utc`

### `enrollment_balance_ledger`

- `id`
- `school_id`
- `enrollment_id`
- `lesson_id` nullable
- `delta_lessons`
- `reason`
- `occurred_at_utc`

Regra preservada:

- aula `Course` consome `1` quando muda para `Realizada`
- se sair de `Realizada`, gera lancamento inverso e devolve saldo

### `lessons`

- `id`
- `school_id`
- `student_id`
- `instructor_id`
- `kind`
- `status`
- `enrollment_id` nullable
- `single_lesson_price` nullable
- `start_at_utc`
- `duration_minutes`
- `notes`
- `created_at_utc`

Regras preservadas:

- `Single` exige preco proprio
- `Course` exige `EnrollmentId`
- `Course` nao usa preco de aula, usa saldo da matricula

## Equipment

### `gear_storages`

- `id`
- `school_id`
- `name`
- `location_note`
- `is_active`
- `created_at_utc`

### `equipment_items`

- `id`
- `school_id`
- `storage_id`
- `name`
- `type`
- `tag_code`
- `brand`
- `model`
- `size_label`
- `current_condition`
- `total_usage_minutes`
- `last_service_date_utc`
- `last_service_usage_minutes`
- `is_active`
- `created_at_utc`

### `lesson_equipment_checkouts`

- `id`
- `school_id`
- `lesson_id`
- `checked_out_at_utc`
- `checked_in_at_utc`
- `created_by_user_id`
- `checked_in_by_user_id`
- `notes_before`
- `notes_after`

### `lesson_equipment_checkout_items`

- `id`
- `school_id`
- `checkout_id`
- `equipment_id`
- `condition_before`
- `condition_after`
- `notes_before`
- `notes_after`

### `equipment_usage_logs`

- `id`
- `school_id`
- `equipment_id`
- `lesson_id`
- `checkout_item_id`
- `usage_minutes`
- `condition_after`
- `recorded_at_utc`

Regras preservadas:

- checkout/checkin por aula registra condicao final
- checkin soma `duration_minutes` da aula ao acumulado do equipamento
- uso por aula fica historico e auditavel

### `maintenance_rules`

- `id`
- `school_id`
- `equipment_type`
- `service_every_minutes` nullable
- `service_every_days` nullable
- `is_active`
- `created_at_utc`

### `maintenance_records`

- `id`
- `school_id`
- `equipment_id`
- `service_date_utc`
- `usage_minutes_at_service`
- `cost`
- `description`
- `performed_by`
- `created_at_utc`

## Finance

### `expense_entries`

- `id`
- `school_id`
- `category`
- `amount`
- `description`
- `vendor`
- `occurred_at_utc`
- `created_at_utc`

### `revenue_entries`

- `id`
- `school_id`
- `source_type`
- `source_id`
- `category`
- `amount`
- `recognized_at_utc`
- `description`
- `created_at_utc`

Uso inicial:

- matricula gera receita de venda
- aula avulsa realizada gera receita operacional
- servico financeiro pode receber esses dados por API na fase inicial

## Reporting

### `report_snapshots`

- `id`
- `school_id`
- `report_name`
- `window_start_utc`
- `window_end_utc`
- `snapshot_version`
- `payload_json`
- `generated_at_utc`
- `expires_at_utc`

Uso atual:

- snapshots por tenant e janela de consulta para dashboards e paineis agregados
- reaproveitamento de leitura recente sem recompor todo o fan-out em tempo real
- base para alertas operacionais e financeiros agregados no proprio `Reporting`

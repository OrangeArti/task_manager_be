# Task Manager Backend
## Security hardening before production
- [ ] Заменить fallback для JWT на «только из конфигурации»:
      var key = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured");
- [ ] Хранить прод-ключи только в секретах окружения/менеджере секретов (без .env в репо).
- [ ] Сгенерировать сильный JWT_KEY (32–64 символов), настроить ротацию.
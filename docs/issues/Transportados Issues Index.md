This page uses the Obsidian Tasks plugin and the canonical `#issue` marker in each local issue file.
# Pendientes

```tasks
path includes issues
tags include #issue
not done
tags do not include #issue/status/cancelled
sort by priority
sort by filename
group by status.type
```

# En Progreso

```tasks
path includes issues
tags include #issue
status.type is IN_PROGRESS
sort by filename reverse
group by tags
```

# Bloqueados

```tasks
path includes issues
tags include #issue
tags include #issue/status/blocked
not done
sort by priority
sort by filename reverse
```

# QA Pendiente

```tasks
path includes issues
tags include #issue
tags include #issue/type/qa
not done
tags do not include #issue/status/cancelled
sort by priority
sort by filename reverse
```

# Alta Prioridad

```tasks
path includes issues
tags include #issue
tag regex matches /#issue\/priority\/(high|urgent)$/
not done
tags do not include #issue/status/cancelled
sort by priority
sort by filename reverse
group by tags
```

# Creados Recientemente

```tasks
path includes issues
tags include #issue
created after 7 days ago
sort by filename reverse
group by status.type
```

# Cerrados Recientemente

```tasks
path includes issues
tags include #issue
done after 14 days ago
sort by done reverse
group by tags
```

# Cancelados Recientemente

```tasks
path includes issues
tags include #issue
cancelled after 30 days ago
sort by cancelled reverse
group by tags
```

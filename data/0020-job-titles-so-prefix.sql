UPDATE public.jobs
SET title = replace(title, 'SO-SO-', 'SO-')
WHERE title LIKE '%SO-SO-%';

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '10s', target: 5 },   // 5 пользователей за 10 сек
        { duration: '20s', target: 5 },   // ƒержим 5 пользователей 20 сек
        { duration: '10s', target: 0 },   // —пускаем за 10 сек
    ],
    thresholds: {
        http_req_duration: ['p(95)<1000'], // 95% запросов < 1 сек
    },
};

const queries = [
    'What is tokenization',
    'How do transformers work',
    'Explain attention mechanisms',
];

export default function () {
    const query = queries[Math.floor(Math.random() * queries.length)];

    const payload = JSON.stringify({
        query: query,
        algorithm: Math.random() > 0.5 ? 'Hybrid' : 'Vector',
        topK: 10,
        useCache: true,
    });

    const params = {
        headers: { 'Content-Type': 'application/json' },
    };

    const res = http.post('http://localhost:5185/Search/Search', payload, params);

    check(res, {
        'status is 200': (r) => r.status === 200,
        'has matches': (r) => JSON.parse(r.body).matches?.length > 0,
    });

    sleep(1); // 1 секунда между запросами
}
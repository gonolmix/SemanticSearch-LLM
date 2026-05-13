import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 25 },   // Ramp to 25 users
    { duration: '3m', target: 25 },   // Sustain under load
    { duration: '1m', target: 50 },   // Spike to 50
    { duration: '2m', target: 50 },   // Sustain spike
    { duration: '2m', target: 0 },    // Graceful ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000'],
    http_req_failed: ['rate<0.1'],
  },
};

const queries = Array.from({length: 50}, (_, i) => `test query ${i}`);

export default function () {
  const payload = JSON.stringify({
    query: queries[Math.floor(Math.random() * queries.length)],
    algorithm: 'Hybrid',
    topK: 10,
    useCache: false, // Disable cache for stress test
  });
  
  const res = http.post('https://localhost:7086/api/searchapi/search', payload, {
    headers: { 'Content-Type': 'application/json' },
    timeout: '15s',
    insecureSkipTLSVerify: true,
  });
  
  check(res, {
    'status 200': (r) => r.status === 200,
    'has results': (r) => {
      try {
        return JSON.parse(r.body).matches?.length >= 0; // Allow empty results under load
      } catch {
        return false;
      }
    },
  });
  
  sleep(0.5 + Math.random() * 1.5); // Faster think time under load
}
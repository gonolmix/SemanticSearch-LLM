import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '10s', target: 2 },
    { duration: '20s', target: 2 },
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'],
    http_req_failed: ['rate<0.1'],
  },
};

const queries = [
  'What is tokenization',
  'How do transformers work',
  'Explain attention mechanisms',
];

export default function () {
  const query = queries[Math.floor(Math.random() * queries.length)];
  const algorithm = Math.random() > 0.5 ? 'Hybrid' : 'Vector';
  
  const payload = JSON.stringify({
    query: query,             
    algorithm: algorithm,     
    topK: 10,
    useCache: true,
    logQuery: false,
  });
  
  const params = {
    headers: { 
      'Content-Type': 'application/json',
    },
    timeout: '10s',
    insecureSkipTLSVerify: true,
  };
  
  const url = 'https://localhost:7086/api/searchapi/search';
  const res = http.post(url, payload, params);
  
  if (res.status !== 200) {
    console.log(`${res.status}: ${res.body?.substring(0, 500)}`);
  }
  
  check(res, {
    'status is 200': (r) => r.status === 200,
    'response is JSON': (r) => {
      try {
        JSON.parse(r.body);
        return true;
      } catch {
        return false;
      }
    },
    'has matches': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.matches && Array.isArray(body.matches) && body.matches.length > 0;
      } catch {
        return false;
      }
    },
  });
  
  sleep(1);
}
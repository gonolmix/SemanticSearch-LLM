import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const searchTime = new Trend('search_time_ms');
const successRate = new Rate('successful_searches');

export const options = {
  stages: [
    { duration: '1m', target: 10 },   // Ramp to 10 users
    { duration: '2m', target: 10 },   // Sustain
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(90)<300', 'p(95)<500'],
    successful_searches: ['rate>0.95'],
    search_time_ms: ['avg<200'],
  },
};

const queries = [
  'best model for image generation',
  'transformer vs CNN for vision',
  'how to fine-tune LLMs',
  'RLHF vs DPO comparison',
  'multimodal model architectures',
];

export default function () {
  group('Search API', function () {
    const payload = JSON.stringify({
      query: queries[Math.floor(Math.random() * queries.length)],
      algorithm: ['Hybrid', 'Vector'][Math.floor(Math.random() * 2)],
      topK: [5, 10, 20][Math.floor(Math.random() * 3)],
      useCache: Math.random() > 0.3,
    });
    
    const startTime = Date.now();
    const res = http.post('https://localhost:7086/api/searchapi/search', payload, {
      headers: { 'Content-Type': 'application/json' },
      timeout: '10s',
      insecureSkipTLSVerify: true,
    });
    const duration = Date.now() - startTime;
    
    searchTime.add(duration);
    
    const success = check(res, {
      'status 200': (r) => r.status === 200,
      'valid JSON': (r) => { try { JSON.parse(r.body); return true; } catch { return false; } },
      'has matches': (r) => JSON.parse(r.body).matches?.length > 0,
      'response < 1s': (r) => duration < 1000,
    });
    
    successRate.add(success);
  });
  
  sleep(1 + Math.random() * 2); // 1-3s think time
}
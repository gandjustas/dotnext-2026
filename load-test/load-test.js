import { check } from 'k6';
import http, { url } from 'k6/http';

const urls = [
    'http://localhost:5001',
    'http://localhost:5002',
    'http://localhost:5003',
    'http://localhost:5004',
];
export const options = {
    duration: '30s',
    vus: 250,
}


function makeRequestAndCheck(url) {
    const a = Math.trunc(Math.random() * 1000000);
    const b = Math.trunc(Math.random() * 1000000);

    var reqUrl = `${url}/test?a=${a}&b=${b}`;
    var r = http.get(reqUrl);
    check(r, { 'status is 200': r.status === 200 });
    check(r, { 'answer is correct': parseInt(r.body) === a + b });
}
//Prewarm cache
export function setup() {
    for (const url of urls) {
        makeRequestAndCheck(url);        
    }

}

export function test() {
    makeRequestAndCheck(__ENV.URL);
}

export default test;

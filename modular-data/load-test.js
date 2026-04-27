import { check } from 'k6';
import http, { url } from 'k6/http';

const urls = [
    'http://localhost:5001',
    'http://localhost:5002',
];
export const options = {
    duration: '30s',
    vus: 50,
}


function makeRequestAndCheck(url) {

    var reqUrl = `${url}/orders/unpaid`;
    var r = http.get(reqUrl);
    check(r, { 'status is 200': r.status === 200 });
}
//Prewarm cache
export function setup() {
    for (let index = 0; index < 5; index++) {        
        for (const url of urls) {
            makeRequestAndCheck(url);        
        }
    }

}

export function test() {
    makeRequestAndCheck(__ENV.URL);
}

export default test;

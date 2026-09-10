// Run against a development server: node scripts/verify-swagger.cjs http://localhost:55174
// Executes the served initializer to catch errors that an HTTP 200 check misses.
const assert = require('node:assert/strict');
const vm = require('node:vm');

async function main() {
    const baseUrl = process.argv[2] || 'http://localhost:55074';
    const documentResponse = await fetch(`${baseUrl}/swagger/v1/swagger.json`);
    assert.equal(documentResponse.status, 200);
    const document = await documentResponse.json();
    const methods = new Set(['get', 'post', 'put', 'patch', 'delete']);
    const operations = Object.values(document.paths)
        .reduce((count, path) => count + Object.keys(path).filter(key => methods.has(key)).length, 0);
    assert.equal(operations, 13, 'All 13 API operations must be documented');

    const initializerResponse = await fetch(`${baseUrl}/swagger/index.js`);
    assert.equal(initializerResponse.status, 200);
    const script = await initializerResponse.text();
    let config;
    let tokenRequests = 0;
    let tokenStatusOk = true;
    const bundle = options => {
        config = options;
        return { initOAuth() {} };
    };
    bundle.presets = { apis: {} };
    const context = {
        window: { location: { href: `${baseUrl}/swagger/index.html`, origin: new URL(baseUrl).origin } },
        URL,
        SwaggerUIBundle: bundle,
        SwaggerUIStandalonePreset: {},
        fetch: async (url, options) => {
            tokenRequests++;
            assert.equal(url, '/api/v1/security/antiforgery');
            assert.equal(options.credentials, 'same-origin');
            return { ok: tokenStatusOk, json: async () => ({ headerName: 'X-CSRF-TOKEN', requestToken: 'test-token' }) };
        }
    };
    vm.runInNewContext(script, context);
    context.window.onload();
    assert.ok(config, 'Swagger UI must initialize without JavaScript errors');
    assert.equal(new URL(config.urls[0].url).pathname, '/swagger/v1/swagger.json');

    for (const request of [
        { url: `${baseUrl}/swagger/v1/swagger.json`, method: 'GET' },
        { url: `${baseUrl}/api/v1/auth/login`, method: 'POST' },
        { url: 'https://example.com/api/v1/orders/1/go', method: 'POST' }
    ]) {
        assert.equal(await config.requestInterceptor(request), request);
    }
    assert.equal(tokenRequests, 0, 'Document, login and external requests must not fetch a CSRF token');
    for (const method of ['POST', 'PUT']) {
        const request = { url: `${baseUrl}/api/v1/risk-profile`, method, headers: { 'Content-Type': 'application/json' } };
        await config.requestInterceptor(request);
        assert.equal(request.headers['X-CSRF-TOKEN'], 'test-token');
        assert.equal(request.headers['Content-Type'], 'application/json');
    }
    assert.equal(tokenRequests, 2, 'Write requests must fetch fresh tokens');
    tokenStatusOk = false;
    await assert.rejects(() => config.requestInterceptor({ url: `${baseUrl}/api/v1/auth/logout`, method: 'POST' }), /Unable to retrieve the CSRF token/);
    console.log(`PASS: ${operations} API operations, Swagger initialization, document URL, CSRF handling and failure handling.`);
}

main().catch(error => { console.error(error); process.exitCode = 1; });

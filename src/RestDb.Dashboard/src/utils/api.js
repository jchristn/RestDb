import i18n from '../i18n';
import { buildCountQuery, buildCountQueryFromExpression } from './sql';

export class ApiError extends Error {
  constructor(statusCode, message, details = '') {
    super(message);
    this.name = 'ApiError';
    this.statusCode = statusCode;
    this.details = details;
  }
}

export default class ApiClient {
  constructor(baseUrl, optionsOrApiKey = '', apiKeyHeader = 'x-api-key', authMode = 'header') {
    const settings =
      typeof optionsOrApiKey === 'object' && optionsOrApiKey !== null
        ? optionsOrApiKey
        : {
            apiKey: optionsOrApiKey,
            apiKeyHeader,
            authMode
          };

    this.baseUrl = (baseUrl || '').trim().replace(/\/$/, '');
    this.apiKey = (settings.apiKey || '').trim();
    this.apiKeyHeader = (settings.apiKeyHeader || '').trim() || 'x-api-key';
    this.authMode = settings.authMode === 'bearer' ? 'bearer' : 'header';
  }

  async probe() {
    return this.getDatabases();
  }

  async getDatabases() {
    return this.request('GET', '/_databases');
  }

  async getDatabase(databaseName) {
    return this.request('GET', `/${encodeURIComponent(databaseName)}`);
  }

  async getDatabaseClients() {
    return this.request('GET', '/_databaseclients');
  }

  async getServerSettings() {
    return this.request('GET', '/_settings');
  }

  async updateServerSettings(settings) {
    return this.request('PUT', '/_settings', {
      body: settings
    });
  }

  async reloadServerSettings() {
    return this.request('POST', '/_settings/reload');
  }

  async getContextDocument() {
    return this.request('GET', '/_context');
  }

  async updateContextDocument(contextDocument) {
    return this.request('PUT', '/_context', {
      body: contextDocument
    });
  }

  async reloadContextDocument() {
    return this.request('POST', '/_context/reload');
  }

  async getDatabaseContext(databaseName) {
    return this.request('GET', `/_context/${encodeURIComponent(databaseName)}`);
  }

  async updateDatabaseContext(databaseName, payload) {
    return this.request('PUT', `/_context/${encodeURIComponent(databaseName)}`, {
      body: payload
    });
  }

  async getTableContext(databaseName, tableName) {
    return this.request('GET', `/_context/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}`);
  }

  async updateTableContext(databaseName, tableName, payload) {
    return this.request('PUT', `/_context/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}`, {
      body: payload
    });
  }

  async getDatabaseDetails(databaseName) {
    return this.request('GET', `/${encodeURIComponent(databaseName)}`, {
      query: {
        _describe: 'true'
      }
    });
  }

  async getTableSchema(databaseName, tableName) {
    return this.request(
      'GET',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}`,
      {
        query: {
          _describe: 'true'
        }
      }
    );
  }

  async getRows(databaseName, tableName, options = {}) {
    const query = {};

    if (Number.isInteger(options.index)) {
      query._index = String(options.index);
    }

    if (Number.isInteger(options.max)) {
      query._max = String(options.max);
    }

    if (options.orderBy) {
      query._order_by = options.orderBy;
    }

    if (options.orderDirection) {
      query._order = options.orderDirection;
    }

    if (options.filters) {
      Object.assign(query, options.filters);
    }

    return this.request(
      'GET',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}`,
      { query }
    );
  }

  async searchRows(databaseName, tableName, expression, options = {}) {
    const query = {};

    if (Number.isInteger(options.index)) {
      query._index = String(options.index);
    }

    if (Number.isInteger(options.max)) {
      query._max = String(options.max);
    }

    if (options.orderBy) {
      query._order_by = options.orderBy;
    }

    if (options.orderDirection) {
      query._order = options.orderDirection;
    }

    return this.request(
      'PUT',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}`,
      {
        body: expression,
        query
      }
    );
  }

  async getRowCount(databaseName, databaseType, tableName, columns, filters = {}, expression = null) {
    const query = expression
      ? buildCountQueryFromExpression(databaseType, tableName, columns, expression)
      : buildCountQuery(databaseType, tableName, columns, filters);
    const result = await this.runRawQuery(databaseName, query);
    const firstRow = Array.isArray(result) ? result[0] : null;
    const recordCount = firstRow?.record_count ?? firstRow?.RECORD_COUNT ?? firstRow?.RecordCount;
    const normalizedCount = Number(recordCount ?? 0);

    return Number.isFinite(normalizedCount) ? normalizedCount : 0;
  }

  async getRow(databaseName, tableName, id) {
    return this.request(
      'GET',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}/${encodeURIComponent(String(id))}`
    );
  }

  async insertRow(databaseName, tableName, payload) {
    return this.request(
      'POST',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}`,
      {
        body: payload
      }
    );
  }

  async updateRow(databaseName, tableName, id, payload) {
    return this.request(
      'PUT',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}/${encodeURIComponent(String(id))}`,
      {
        body: payload
      }
    );
  }

  async deleteRow(databaseName, tableName, id) {
    return this.request(
      'DELETE',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}/${encodeURIComponent(String(id))}`
    );
  }

  async truncateTable(databaseName, tableName) {
    return this.request(
      'DELETE',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}`,
      {
        query: {
          _truncate: 'true'
        }
      }
    );
  }

  async dropTable(databaseName, tableName) {
    return this.request(
      'DELETE',
      `/${encodeURIComponent(databaseName)}/${encodeURIComponent(tableName)}`,
      {
        query: {
          _drop: 'true'
        }
      }
    );
  }

  async runRawQuery(databaseName, sql) {
    return this.request('POST', `/${encodeURIComponent(databaseName)}`, {
      query: {
        raw: 'true'
      },
      contentType: 'text/plain',
      rawBody: sql
    });
  }

  async request(method, path, options = {}) {
    if (!this.baseUrl) {
      throw new ApiError(0, 'A RestDb server URL is required.');
    }

    const url = new URL(this.baseUrl + path);
    if (options.query) {
      Object.entries(options.query).forEach(([key, value]) => {
        if (value === undefined || value === null || value === '') {
          return;
        }

        url.searchParams.set(key, value);
      });
    }

    const headers = {
      Accept: 'application/json',
      'Accept-Language': i18n.resolvedLanguage || i18n.language || 'en-US'
    };

    if (this.apiKey) {
      if (this.authMode === 'bearer') {
        headers.Authorization = `Bearer ${this.apiKey}`;
      } else {
        headers[this.apiKeyHeader] = this.apiKey;
      }
    }

    if (options.contentType) {
      headers['Content-Type'] = options.contentType;
    } else if (options.rawBody !== undefined || options.body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    if (options.headers) {
      Object.assign(headers, options.headers);
    }

    const response = await fetch(url.toString(), {
      method,
      headers,
      body:
        options.rawBody !== undefined
          ? options.rawBody
          : options.body !== undefined
            ? JSON.stringify(options.body)
            : undefined
    });

    if (!response.ok) {
      const errorText = await response.text().catch(() => '');
      throw new ApiError(
        response.status,
        errorText || `Request failed with status ${response.status}.`,
        errorText
      );
    }

    if (response.status === 204) {
      return null;
    }

    const responseText = await response.text();
    if (!responseText) {
      return null;
    }

    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
      return JSON.parse(responseText);
    }

    return responseText;
  }
}

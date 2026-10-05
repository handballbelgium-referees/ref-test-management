import { FactoryProvider, LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApolloClient, ApolloLink, InMemoryCache } from '@apollo/client';
import { parse } from 'graphql';
import { createClient } from 'graphql-sse';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { appConfig, createSseLink } from './app.config';
import { LanguageConfig } from './services/language-config';

const subscriptionOperations = [
  {
    name: 'RefTestTimeExtended',
    field: 'refTestTimeExtended',
    document:
      'subscription RefTestTimeExtended($id: ID!) { refTestTimeExtended(id: $id) { id } }',
    variables: { id: 'ref-test-id' },
  },
  {
    name: 'RefTestUpdated',
    field: 'refTestUpdated',
    document: 'subscription RefTestUpdated($id: ID!) { refTestUpdated(id: $id) { __typename } }',
    variables: { id: 'ref-test-id' },
  },
  {
    name: 'RefTestsUpdated',
    field: 'refTestsUpdated',
    document: 'subscription RefTestsUpdated { refTestsUpdated { __typename } }',
    variables: {},
  },
  {
    name: 'RefTestSessionLock',
    field: 'refTestSessionLock',
    document:
      'subscription RefTestSessionLock($token: String!, $sessionId: String!) { refTestSessionLock(token: $token, sessionId: $sessionId) { status } }',
    variables: { token: 'test-token', sessionId: 'test-session' },
  },
] as const;

interface TestSseConnection {
  readonly signal?: AbortSignal;
  send(result: unknown): void;
  complete(): void;
  close(): void;
}

class TestSseTransport {
  readonly connections: TestSseConnection[] = [];
  private _activeConnections = 0;

  get activeConnections(): number {
    return this._activeConnections;
  }

  readonly fetch: typeof fetch = async (_input, init) => {
    let controller!: ReadableStreamDefaultController<Uint8Array>;
    let isOpen = true;
    const body = new ReadableStream<Uint8Array>({
      start: (streamController) => {
        controller = streamController;
      },
    });
    this._activeConnections++;

    const finish = (close: boolean) => {
      if (!isOpen) return;
      isOpen = false;
      this._activeConnections--;
      if (close) controller.close();
      else controller.error(new Error('Connection aborted'));
    };
    const sendEvent = (event: string, data?: unknown) => {
      const encodedData = data === undefined ? '' : `data: ${JSON.stringify(data)}\n`;
      controller.enqueue(new TextEncoder().encode(`event: ${event}\n${encodedData}\n`));
    };
    const connection: TestSseConnection = {
      signal: init?.signal ?? undefined,
      send: (result) => {
        if (isOpen) sendEvent('next', result);
      },
      complete: () => {
        if (isOpen) sendEvent('complete');
      },
      close: () => finish(true),
    };
    this.connections.push(connection);

    if (init?.signal?.aborted) finish(false);
    else init?.signal?.addEventListener('abort', () => finish(false), { once: true });

    return { ok: true, status: 200, statusText: 'OK', body } as Response;
  };
}

class TestVisibilityDocument extends EventTarget {
  visibilityState: DocumentVisibilityState = 'hidden';

  show(): void {
    this.visibilityState = 'visible';
    this.dispatchEvent(new Event('visibilitychange'));
  }
}

function createTestClient(transport: TestSseTransport) {
  return createClient({
    url: '/graphql',
    fetchFn: transport.fetch,
    retryAttempts: Infinity,
    retry: async () => {},
  });
}

function executeSubscription(
  link: ApolloLink,
  client: ApolloClient,
  document: string,
  variables: Record<string, string>,
) {
  return ApolloLink.execute(
    link,
    { query: parse(document), variables },
    { client },
  );
}

describe('app locale configuration', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('derives LOCALE_ID from the resolved language', () => {
    const localeIdProvider = appConfig.providers?.find(
      (provider): provider is FactoryProvider =>
        typeof provider === 'object' &&
        provider !== null &&
        'provide' in provider &&
        provider.provide === LOCALE_ID,
    );

    if (!localeIdProvider) {
      throw new Error('LOCALE_ID provider is not configured.');
    }

    TestBed.configureTestingModule({
      providers: [
        localeIdProvider,
        { provide: LanguageConfig, useValue: { initialLanguage: 'fr' } },
      ],
    });

    expect(TestBed.inject(LOCALE_ID)).toBe('fr-BE');
  });
});

describe('shared GraphQL SSE link', () => {
  it.each(subscriptionOperations)(
    'delivers multiple events for $name without completing the operation',
    async ({ field, document, variables }) => {
      const transport = new TestSseTransport();
      const visibilityDocument = new TestVisibilityDocument();
      const sseClient = createTestClient(transport);
      const link = createSseLink(sseClient, visibilityDocument);
      const client = new ApolloClient({ cache: new InMemoryCache(), link });
      const received: unknown[] = [];
      let completed = false;
      let error: unknown;
      const subscription = executeSubscription(link, client, document, variables).subscribe({
        next: (result) => received.push(result),
        error: (value) => (error = value),
        complete: () => (completed = true),
      });

      try {
        await vi.waitFor(() => expect(transport.connections).toHaveLength(1));
        const first = { data: { [field]: { update: 1 } } };
        const second = { data: { [field]: { update: 2 } } };
        transport.connections[0].send(first);
        transport.connections[0].send(second);

        await vi.waitFor(() => expect(received).toHaveLength(2));
        expect(received).toEqual([first, second]);
        expect(error).toBeUndefined();
        expect(completed).toBe(false);
        subscription.unsubscribe();
        expect(transport.connections[0].signal?.aborted).toBe(true);
        expect(transport.activeConnections).toBe(0);
      } finally {
        subscription.unsubscribe();
        client.stop();
        sseClient.dispose();
      }

      expect(transport.activeConnections).toBe(0);
      visibilityDocument.show();
      expect(transport.connections).toHaveLength(1);
    },
  );

  it('keeps one active stream and delivers events across a transient transport reconnect', async () => {
    const transport = new TestSseTransport();
    const visibilityDocument = new TestVisibilityDocument();
    const sseClient = createTestClient(transport);
    const link = createSseLink(sseClient, visibilityDocument);
    const client = new ApolloClient({ cache: new InMemoryCache(), link });
    const operation = subscriptionOperations[0];
    const received: unknown[] = [];
    let completed = false;
    const subscription = executeSubscription(
      link,
      client,
      operation.document,
      operation.variables,
    ).subscribe({
      next: (result) => received.push(result),
      complete: () => (completed = true),
    });

    try {
      await vi.waitFor(() => expect(transport.connections).toHaveLength(1));
      transport.connections[0].send({ data: { [operation.field]: { update: 1 } } });
      await vi.waitFor(() => expect(received).toHaveLength(1));
      await new Promise((resolve) => setTimeout(resolve, 0));
      transport.connections[0].close();
      await vi.waitFor(() => expect(transport.connections).toHaveLength(2));
      transport.connections[1].send({ data: { [operation.field]: { update: 2 } } });

      await vi.waitFor(() => expect(received).toHaveLength(2));
      expect(completed).toBe(false);
      expect(transport.activeConnections).toBe(1);
    } finally {
      subscription.unsubscribe();
      client.stop();
      sseClient.dispose();
    }

    expect(transport.activeConnections).toBe(0);
  });

  it('restarts on visibility and ignores completion from the replaced stream', async () => {
    const transport = new TestSseTransport();
    const visibilityDocument = new TestVisibilityDocument();
    const sseClient = createTestClient(transport);
    const link = createSseLink(sseClient, visibilityDocument);
    const client = new ApolloClient({ cache: new InMemoryCache(), link });
    const operation = subscriptionOperations[0];
    const received: unknown[] = [];
    let completed = false;
    const subscription = executeSubscription(
      link,
      client,
      operation.document,
      operation.variables,
    ).subscribe({
      next: (result) => received.push(result),
      complete: () => (completed = true),
    });

    try {
      await vi.waitFor(() => expect(transport.connections).toHaveLength(1));
      transport.connections[0].send({ data: { [operation.field]: { update: 1 } } });
      await vi.waitFor(() => expect(received).toHaveLength(1));

      visibilityDocument.show();
      await vi.waitFor(() => expect(transport.connections).toHaveLength(2));
      expect(transport.connections[0].signal?.aborted).toBe(true);
      expect(transport.activeConnections).toBe(1);
      expect(completed).toBe(false);
      transport.connections[1].send({ data: { [operation.field]: { update: 2 } } });

      await vi.waitFor(() => expect(received).toHaveLength(2));
      expect(completed).toBe(false);
    } finally {
      subscription.unsubscribe();
      client.stop();
      sseClient.dispose();
    }

    expect(transport.activeConnections).toBe(0);
    visibilityDocument.show();
    expect(transport.connections).toHaveLength(2);
  });

  it.each([
    {
      name: 'session-lock completion',
      operation: subscriptionOperations[3],
      result: { data: { refTestSessionLock: { status: 'BLOCKED' } } },
    },
    {
      name: 'authorization error',
      operation: subscriptionOperations[1],
      result: { errors: [{ message: 'The subscription permission is no longer available.' }] },
    },
  ])('preserves terminal $name without retrying', async ({ operation, result }) => {
    const transport = new TestSseTransport();
    const visibilityDocument = new TestVisibilityDocument();
    const sseClient = createTestClient(transport);
    const link = createSseLink(sseClient, visibilityDocument);
    const client = new ApolloClient({ cache: new InMemoryCache(), link });
    const received: unknown[] = [];
    let completed = false;
    const subscription = executeSubscription(
      link,
      client,
      operation.document,
      operation.variables,
    ).subscribe({
      next: (value) => received.push(value),
      complete: () => (completed = true),
    });

    try {
      await vi.waitFor(() => expect(transport.connections).toHaveLength(1));
      transport.connections[0].send(result);
      transport.connections[0].complete();

      await vi.waitFor(() => expect(completed).toBe(true));
      expect(received).toEqual([result]);
      expect(transport.connections).toHaveLength(1);
    } finally {
      subscription.unsubscribe();
      client.stop();
      sseClient.dispose();
    }

    expect(transport.activeConnections).toBe(0);
  });
});

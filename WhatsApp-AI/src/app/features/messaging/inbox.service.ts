import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { APP_CONFIG } from '../../core/config/app-config';
import { ConversationMessageDto, ConversationThreadDto } from './conversation.models';

@Injectable({ providedIn: 'root' })
export class InboxService {
  private readonly apiClient = inject(ApiClientService);

  getThread(tenantId: string, threadId: string): Observable<ConversationThreadDto> {
    return this.apiClient.get<ConversationThreadDto>(
      APP_CONFIG.conversations.getThread(threadId), { tenantId });
  }

  listThreads(tenantId: string, pageIndex = 0, pageSize = 50): Observable<ConversationThreadDto[]> {
    return this.apiClient.get<ConversationThreadDto[]>(APP_CONFIG.conversations.list, {
      tenantId,
      pageIndex,
      pageSize,
    });
  }

  getMessages(
    tenantId: string,
    threadId: string,
    pageIndex = 0,
    pageSize = 200,
  ): Observable<ConversationMessageDto[]> {
    return this.apiClient.get<ConversationMessageDto[]>(
      APP_CONFIG.conversations.messages(threadId),
      { tenantId, pageIndex, pageSize },
    );
  }
}

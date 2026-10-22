import { Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { ConversationResponse } from '../../core/api';
import { Conversations } from '../../core/conversations';

@Component({
  imports: [DatePipe, MatButtonModule, MatIconModule, MatListModule],
  selector: 'app-conversation-list',
  styleUrl: './conversation-list.css',
  templateUrl: './conversation-list.html',
})
export class ConversationList implements OnInit {
  protected readonly conversations = inject(Conversations);

  ngOnInit(): void {
    this.conversations.load();
  }

  protected deleteConversation(event: Event, conversation: ConversationResponse): void {
    // The button sits inside the list item: without this, the click would also open it.
    event.stopPropagation();
    if (!confirm(`Delete "${conversation.title}"? Its questions and answers will be lost.`)) {
      return;
    }

    this.conversations.delete(conversation.conversationId);
  }
}

import { Component, signal } from '@angular/core';
import { Upload } from './features/upload/upload';
import { Query } from './features/query/query';

@Component({
  imports: [Upload, Query],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('DocMind');
}

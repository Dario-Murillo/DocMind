import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Conversations } from '../../core/conversations';
import { Query } from './query';

describe('Query', () => {
  let component: Query;
  let fixture: ComponentFixture<Query>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Query],
      // Workspace provides Conversations in the app (it isn't auto-provided), so the test does too.
      providers: [provideHttpClient(), provideHttpClientTesting(), Conversations],
    }).compileComponents();

    fixture = TestBed.createComponent(Query);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
